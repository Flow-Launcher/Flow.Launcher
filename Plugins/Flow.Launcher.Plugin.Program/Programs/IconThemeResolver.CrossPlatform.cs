using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.Program.Logger;

namespace Flow.Launcher.Plugin.Program.Programs
{
    /// <summary>
    /// Resolves the Icon key of desktop entries to image files following the freedesktop Icon Theme spec
    /// (https://specifications.freedesktop.org/icon-theme-spec/latest/): the user's theme and the themes it
    /// inherits from, then hicolor, then the pixmaps folders. Scalable SVGs are preferred, then the largest PNG;
    /// XPM is skipped because the host can't render it. One instance serves one index run and caches per icon name.
    /// </summary>
    internal sealed class IconThemeResolver
    {
        private const string FallbackTheme = "hicolor";
        private const string ApplicationsContext = "Applications";
        private const string SvgExtension = ".svg";
        private const string PngExtension = ".png";

        // What the host's image loader can render
        private static readonly string[] SupportedExtensions =
            [SvgExtension, PngExtension, ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico"];

        private static readonly string[] ThemeIconExtensions = [SvgExtension, PngExtension];

        private readonly List<IconTheme> _themes;
        private readonly List<string> _pixmapDirectories;
        private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

        /// <param name="dataDirectories">XDG data directories, most important first ($XDG_DATA_HOME first).</param>
        /// <param name="currentDesktops">Entries of $XDG_CURRENT_DESKTOP, used to guess the default theme.</param>
        public IconThemeResolver(IReadOnlyList<string> dataDirectories, IReadOnlyList<string> currentDesktops)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // ~/.icons comes first for backwards compatibility, as the spec requires
            var iconBaseDirectories = new[] { Path.Combine(home, ".icons") }
                .Concat(dataDirectories.Select(d => Path.Combine(d, "icons")))
                .Distinct(StringComparer.Ordinal)
                .Where(Directory.Exists)
                .ToList();

            _pixmapDirectories = dataDirectories.Select(d => Path.Combine(d, "pixmaps"))
                .Append("/usr/share/pixmaps")
                .Distinct(StringComparer.Ordinal)
                .Where(Directory.Exists)
                .ToList();

            _themes = LoadThemeChain(GetUserThemeName(currentDesktops), iconBaseDirectories);
        }

        /// <summary>
        /// Path of the image for an Icon value, or null if none was found in a format the host can render.
        /// </summary>
        public string Resolve(string icon)
        {
            return string.IsNullOrWhiteSpace(icon) ? null : _cache.GetOrAdd(icon.Trim(), Lookup);
        }

        private string Lookup(string icon)
        {
            if (Path.IsPathRooted(icon))
            {
                if (File.Exists(icon) && IsSupported(icon))
                    return icon;

                // E.g. an absolute .xpm: try a renderable icon of the same name instead
                icon = Path.GetFileName(icon);
            }

            // Icon names shouldn't carry an extension, but some entries do (e.g. "app.png")
            var extension = Path.GetExtension(icon).ToLowerInvariant();
            var name = extension is SvgExtension or PngExtension or ".xpm" ? Path.GetFileNameWithoutExtension(icon) : icon;

            foreach (var theme in _themes)
            {
                if (theme.Find(name) is { } path)
                    return path;
            }

            foreach (var directory in _pixmapDirectories)
            {
                foreach (var candidateExtension in ThemeIconExtensions)
                {
                    var path = Path.Combine(directory, name + candidateExtension);
                    if (File.Exists(path))
                        return path;
                }

                if (name != icon && IsSupported(icon) && File.Exists(Path.Combine(directory, icon)))
                    return Path.Combine(directory, icon);
            }

            return null;
        }

        private static bool IsSupported(string path)
        {
            return SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        /// <summary>
        /// The configured icon theme. Read from config files rather than gsettings/kreadconfig so indexing never
        /// waits on another process.
        /// </summary>
        private static string GetUserThemeName(IReadOnlyList<string> currentDesktops)
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome) || !Path.IsPathRooted(configHome))
                configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

            var configDirectories = (Environment.GetEnvironmentVariable("XDG_CONFIG_DIRS") ?? string.Empty)
                .Split(':', StringSplitOptions.RemoveEmptyEntries)
                .Where(Path.IsPathRooted)
                .DefaultIfEmpty("/etc/xdg");

            var isKde = currentDesktops.Contains("KDE", StringComparer.OrdinalIgnoreCase);

            string FromKde() => ReadSetting(Path.Combine(configHome, "kdeglobals"), "Icons", "Theme");

            string FromGtk() =>
                new[] { Path.Combine(configHome, "gtk-3.0", "settings.ini"), Path.Combine(configHome, "gtk-4.0", "settings.ini") }
                    .Concat(configDirectories.Select(d => Path.Combine(d, "gtk-3.0", "settings.ini")))
                    .Select(file => ReadSetting(file, "Settings", "gtk-icon-theme-name"))
                    .FirstOrDefault(theme => theme != null);

            // Qt apps outside KDE (common on wlroots compositors) are configured through qt5ct/qt6ct
            string FromQtCt() =>
                new[] { Path.Combine(configHome, "qt6ct", "qt6ct.conf"), Path.Combine(configHome, "qt5ct", "qt5ct.conf") }
                    .Select(file => ReadSetting(file, "Appearance", "icon_theme"))
                    .FirstOrDefault(theme => theme != null);

            var configured = isKde ? FromKde() ?? FromGtk() ?? FromQtCt() : FromGtk() ?? FromKde() ?? FromQtCt();

            // The toolkit defaults when nothing is configured
            return configured ?? (isKde ? "breeze" : "Adwaita");
        }

        private static string ReadSetting(string file, string group, string key)
        {
            try
            {
                if (!File.Exists(file))
                    return null;

                return KeyFile.Read(file, group).TryGetValue(group, out var values) &&
                       values.TryGetValue(key, out var value) &&
                       !string.IsNullOrWhiteSpace(value)
                    ? value.Trim().Trim('"')
                    : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                ProgramLogger.LogDebug(nameof(IconThemeResolver), nameof(ReadSetting), file, $"Failed to read: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// The theme, its parents depth-first (the order the spec's lookup visits them), and hicolor last.
        /// </summary>
        private static List<IconTheme> LoadThemeChain(string themeName, IReadOnlyList<string> baseDirectories)
        {
            var themes = new List<IconTheme>();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            void Visit(string name)
            {
                if (themes.Count > 32 || !visited.Add(name))
                    return;

                var theme = IconTheme.Load(name, baseDirectories);
                if (theme == null)
                    return;

                themes.Add(theme);
                foreach (var parent in theme.Inherits.Where(p => p != FallbackTheme))
                {
                    Visit(parent);
                }
            }

            Visit(themeName);
            visited.Remove(FallbackTheme);
            Visit(FallbackTheme);
            return themes;
        }

        private sealed record IconDirectory(string Name, int Size, int Scale, bool Scalable, bool IsApplications);

        private sealed class IconTheme
        {
            private readonly string[] _roots;
            private readonly IconDirectory[] _directories;

            public IReadOnlyList<string> Inherits { get; }

            private IconTheme(string[] roots, IconDirectory[] directories, IReadOnlyList<string> inherits)
            {
                _roots = roots;
                _directories = directories;
                Inherits = inherits;
            }

            /// <summary>
            /// A theme may be spread over several base directories; the first index.theme describes it.
            /// </summary>
            public static IconTheme Load(string name, IReadOnlyList<string> baseDirectories)
            {
                var roots = baseDirectories.Select(b => Path.Combine(b, name)).Where(Directory.Exists).ToArray();
                var index = roots.Select(r => Path.Combine(r, "index.theme")).FirstOrDefault(File.Exists);
                if (index == null)
                    return null;

                Dictionary<string, Dictionary<string, string>> groups;
                try
                {
                    groups = KeyFile.Read(index);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    ProgramLogger.LogException($"|IconThemeResolver|Load|{index}|Failed to read the icon theme", e);
                    return null;
                }

                if (!groups.TryGetValue("Icon Theme", out var header))
                    return null;

                var directoryNames = SplitCommaList(header, "Directories").Concat(SplitCommaList(header, "ScaledDirectories"))
                    .Distinct(StringComparer.Ordinal);

                var directories = new List<IconDirectory>();
                foreach (var directoryName in directoryNames)
                {
                    if (!groups.TryGetValue(directoryName, out var values) || !TryGetInt(values, "Size", out var size))
                        continue;

                    var scale = TryGetInt(values, "Scale", out var s) && s > 0 ? s : 1;
                    var scalable = values.TryGetValue("Type", out var type) && type.Trim() == "Scalable";
                    var isApplications = values.TryGetValue("Context", out var context) && context.Trim() == ApplicationsContext;
                    directories.Add(new IconDirectory(directoryName, size, scale, scalable, isApplications));
                }

                return new IconTheme(roots, directories.ToArray(), SplitCommaList(header, "Inherits"));
            }

            /// <summary>
            /// Best match for <paramref name="iconName"/> in this theme. Application directories are searched first
            /// so large themes don't need a file check per directory for every app.
            /// </summary>
            public string Find(string iconName)
            {
                return FindIn(iconName, true) ?? FindIn(iconName, false);
            }

            private string FindIn(string iconName, bool applications)
            {
                string best = null;
                (int Kind, int Size) bestRank = default;

                foreach (var directory in _directories)
                {
                    if (directory.IsApplications != applications)
                        continue;

                    foreach (var root in _roots)
                    {
                        foreach (var extension in ThemeIconExtensions)
                        {
                            var path = Path.Combine(root, directory.Name, iconName + extension);
                            if (!File.Exists(path))
                                continue;

                            // SVG beats PNG; within a kind, scalable/larger beats smaller
                            var rank = (Kind: extension == SvgExtension ? 2 : 1,
                                        Size: directory.Scalable ? int.MaxValue : directory.Size * directory.Scale);
                            if (best == null || rank.CompareTo(bestRank) > 0)
                            {
                                best = path;
                                bestRank = rank;
                            }
                        }
                    }
                }

                return best;
            }

            private static IReadOnlyList<string> SplitCommaList(Dictionary<string, string> values, string key)
            {
                return values.TryGetValue(key, out var value)
                    ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : Array.Empty<string>();
            }

            private static bool TryGetInt(Dictionary<string, string> values, string key, out int result)
            {
                result = 0;
                return values.TryGetValue(key, out var value) && int.TryParse(value.Trim(), out result);
            }
        }
    }
}
