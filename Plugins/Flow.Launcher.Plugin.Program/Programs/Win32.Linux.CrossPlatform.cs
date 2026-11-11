using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Program.Logger;
using Flow.Launcher.Plugin.Program.Views.Models;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Program.Programs
{
    /// <summary>
    /// Linux program source: freedesktop desktop entries (*.desktop) are indexed as <see cref="Win32"/> entries so
    /// the plugin's cache, disabling and scoring work unchanged. <see cref="FullPath"/> is the .desktop file,
    /// <see cref="LnkResolvedPath"/> the resolved executable and <see cref="ExecutableName"/> the extra search terms
    /// (binary name, GenericName, Keywords). Launching re-reads the file so edits apply without re-indexing.
    /// </summary>
    public partial class Win32
    {
        private const string DesktopEntryExtension = "desktop";

        // Terminals tried when Terminal=true, with the arguments that precede the command to run
        private static readonly (string Command, string[] Arguments)[] KnownTerminals =
        [
            ("x-terminal-emulator", ["-e"]),
            ("kitty", []),
            ("alacritty", ["-e"]),
            ("foot", ["--"]),
            ("wezterm", ["start", "--"]),
            ("gnome-terminal", ["--"]),
            ("konsole", ["-e"]),
            ("xfce4-terminal", ["-x"]),
            ("xterm", ["-e"]),
        ];

        private static readonly EnumerationOptions DesktopDirectoryEnumerationOptions = new()
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.None
        };

        [SupportedOSPlatform("linux")]
        private static Win32[] AllDesktopEntries(Settings settings)
        {
            try
            {
                // Disabled custom sources are not in DisabledProgramSources
                var directories = DesktopEntryIndex.GetApplicationDirectories()
                    .Concat(settings.ProgramSources.Where(s => s.Enabled).Select(s => s.Location))
                    .Where(Directory.Exists)
                    .Distinct(StringComparer.Ordinal);

                var index = new DesktopEntryIndex();

                // Remove disabled programs in DisabledProgramSources
                return ExceptDisabledSource(EnumerateDesktopFilesById(directories))
                    .AsParallel()
                    .Select(index.CreateProgram)
                    .Where(x => x is { Valid: true })
                    .Distinct()
                    .ToArray();
            }
            catch (Exception e)
            {
                ProgramLogger.LogException("|Win32|AllDesktopEntries|Not available|An unexpected error occurred", e);

                return Array.Empty<Win32>();
            }
        }

        /// <summary>
        /// Desktop files of all <paramref name="directories"/>, keeping only the first file for each desktop file ID
        /// (path relative to its applications folder with '/' replaced by '-'), so user entries override system ones.
        /// </summary>
        private static List<string> EnumerateDesktopFilesById(IEnumerable<string> directories)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var files = new List<string>();

            foreach (var directory in directories)
            {
                foreach (var file in EnumerateDesktopFiles(directory))
                {
                    var id = Path.GetRelativePath(directory, file).Replace('/', '-');
                    if (ids.Add(id))
                    {
                        files.Add(file);
                    }
                }
            }

            return files;
        }

        /// <summary>
        /// Finds *.desktop files under <paramref name="directory"/> recursively. Symbolic links are followed because
        /// profile-based distributions (e.g. NixOS) link whole folders, so each real folder is visited only once.
        /// </summary>
        private static IEnumerable<string> EnumerateDesktopFiles(string directory)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(directory));

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(GetRealPath(current)))
                    continue;

                FileSystemInfo[] entries;
                try
                {
                    entries = current.GetFileSystemInfos("*", DesktopDirectoryEnumerationOptions);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    ProgramLogger.LogException($"|Win32|EnumerateDesktopFiles|{current.FullName}" +
                                               "|Failed to list the folder", e);
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (entry is DirectoryInfo subDirectory)
                    {
                        pending.Push(subDirectory);
                    }
                    else if (entry.LinkTarget != null && Directory.Exists(entry.FullName))
                    {
                        pending.Push(new DirectoryInfo(entry.FullName));
                    }
                    else if (Extension(entry.Name) == DesktopEntryExtension)
                    {
                        yield return entry.FullName;
                    }
                }
            }
        }

        private static string GetRealPath(DirectoryInfo directory)
        {
            try
            {
                return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName;
            }
            catch (IOException)
            {
                return directory.FullName;
            }
        }

        [SupportedOSPlatform("linux")]
        private static void WatchDesktopEntryDirectories(Settings settings)
        {
            var sources = DesktopEntryIndex.GetApplicationDirectories()
                .Select(directory => new ProgramSource(directory))
                .Concat(settings.ProgramSources);

            var watchedLinks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var directory in from path in GetCommonParents(sources) where Directory.Exists(path) select path)
            {
                TryWatch(directory, $"*.{DesktopEntryExtension}", includeSubdirectories: true);

                foreach (var (parent, name) in GetSymbolicLinksOnPath(directory))
                {
                    if (watchedLinks.Add(Path.Combine(parent, name)))
                    {
                        TryWatch(parent, name, includeSubdirectories: false);
                    }
                }
            }

            _ = Task.Run(MonitorDirectoryChangeAsync);
        }

        /// <summary>
        /// Watches like <see cref="WatchDirectory"/>, but also reacts to renames and edits: package managers install
        /// desktop files by renaming a temporary file, and users hide entries by editing them.
        /// </summary>
        private static void TryWatch(string directory, string filter, bool includeSubdirectories)
        {
            try
            {
                var watcher = new FileSystemWatcher(directory, filter)
                {
                    IncludeSubdirectories = includeSubdirectories,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                };

                watcher.Created += static (_, _) => indexQueue.Writer.TryWrite(default);
                watcher.Deleted += static (_, _) => indexQueue.Writer.TryWrite(default);
                watcher.Changed += static (_, _) => indexQueue.Writer.TryWrite(default);
                watcher.Renamed += static (_, _) => indexQueue.Writer.TryWrite(default);
                watcher.EnableRaisingEvents = true;

                Watchers.Add(watcher);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // E.g. the inotify watch limit is exhausted
                ProgramLogger.LogException($"|Win32|TryWatch|{directory}|Failed to watch {filter}", e);
            }
        }

        /// <summary>
        /// Symbolic links on the way to <paramref name="path"/> as (containing folder, link name). Profile-based
        /// package managers (Nix, Guix) install apps by atomically replacing such a link (e.g. /run/current-system)
        /// instead of touching the applications folder, so the watcher on the folder itself never fires.
        /// </summary>
        private static List<(string Parent, string Name)> GetSymbolicLinksOnPath(string path)
        {
            // Store paths never change, and watching the store itself would wake us up for every package build
            var storeDirectory = Environment.GetEnvironmentVariable("NIX_STORE_DIR") ?? "/nix/store";

            var links = new List<(string, string)>();
            var remaining = new Queue<string>(path.Split('/', StringSplitOptions.RemoveEmptyEntries));
            var current = "/";

            // Bounded like the kernel's symlink resolution to survive link loops
            for (var hops = 0; remaining.Count > 0 && hops < 40;)
            {
                var name = remaining.Dequeue();
                var next = Path.Combine(current, name);

                string target;
                try
                {
                    target = new FileInfo(next).LinkTarget;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    target = null;
                }

                if (target == null)
                {
                    current = next;
                    continue;
                }

                hops++;
                if (!(current + "/").StartsWith(storeDirectory.TrimEnd('/') + "/", StringComparison.Ordinal))
                {
                    links.Add((current, name));
                }

                // Continue from the link target, which may contain links itself
                var resolved = Path.GetFullPath(target, current);
                remaining = new Queue<string>(resolved.Split('/', StringSplitOptions.RemoveEmptyEntries).Concat(remaining));
                current = "/";
            }

            return links;
        }

        [SupportedOSPlatform("linux")]
        private void LaunchDesktopEntry()
        {
            _ = Task.Run(() =>
            {
                var info = CreateDesktopEntryStartInfo(out var error);
                if (info == null)
                {
                    Main.Context.API.ShowMsgError(
                        Main.Context.API.GetTranslation("flowlauncher_plugin_program_disable_dlgtitle_error"), error);
                    return;
                }

                // Desktop file id (without ".desktop") names the app's systemd scope.
                var applicationId = Path.GetFileNameWithoutExtension(FullPath);
                Main.StartProcess(startInfo =>
                {
                    AppProcess.Start(startInfo, applicationId)?.Dispose();
                    return null;
                }, info);
            });
        }

        [SupportedOSPlatform("linux")]
        private ProcessStartInfo CreateDesktopEntryStartInfo(out string error)
        {
            var runFailed = string.Format(Main.Context.API.GetTranslation("flowlauncher_plugin_program_run_failed"), FullPath);

            DesktopEntry entry;
            try
            {
                entry = DesktopEntry.Load(FullPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                ProgramLogger.LogException($"|Win32|CreateDesktopEntryStartInfo|{FullPath}|Failed to read the desktop entry", e);
                entry = null;
            }

            var locales = DesktopEntry.GetLocaleCandidates(CultureInfo.CurrentUICulture);
            var arguments = entry?.ExpandExec(entry.GetLocaleString("Name", locales) ?? Name, locales);
            if (arguments == null || arguments.Count == 0)
            {
                error = runFailed;
                return null;
            }

            if (entry.GetBoolean("Terminal"))
            {
                var terminal = FindTerminal();
                if (terminal == null)
                {
                    error = string.Format(Main.Context.API.GetTranslation("flowlauncher_plugin_program_no_terminal"), Name);
                    return null;
                }

                arguments.InsertRange(0, terminal);
            }

            // Don't let apps inherit Flow's working directory
            var workingDirectory = entry.GetString("Path");
            if (workingDirectory == null || !Directory.Exists(workingDirectory))
            {
                workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            var info = new ProcessStartInfo(arguments[0])
            {
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            };
            foreach (var argument in arguments.Skip(1))
            {
                info.ArgumentList.Add(argument);
            }

            error = null;
            return info;
        }

        /// <summary>
        /// Command prefix that runs a program in a terminal: $TERMINAL if set, otherwise the first known terminal
        /// found on PATH.
        /// </summary>
        [SupportedOSPlatform("linux")]
        private static List<string> FindTerminal()
        {
            var configured = Environment.GetEnvironmentVariable("TERMINAL");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var parts = DesktopEntry.SplitCommandLine(configured);
                if (parts.Count > 0 && ExecutableLookup.Find(parts[0]) is { } path)
                {
                    // Unknown terminals get the common -e convention
                    var known = KnownTerminals.FirstOrDefault(t => t.Command == Path.GetFileName(path));
                    return [path, .. parts.Skip(1), .. (known.Arguments ?? ["-e"])];
                }

                ProgramLogger.LogDebug(nameof(Win32), nameof(FindTerminal), configured, "TERMINAL was not found on PATH");
            }

            foreach (var (command, arguments) in KnownTerminals)
            {
                if (ExecutableLookup.Find(command) is { } path)
                    return [path, .. arguments];
            }

            return null;
        }

        private void OpenDesktopEntryFolder()
        {
            Main.Context.API.OpenDirectory(ParentDirectory, FullPath);
        }
    }

    /// <summary>
    /// State shared by one Linux index run: locale, desktop environment, icon and executable lookups.
    /// </summary>
    [SupportedOSPlatform("linux")]
    internal sealed class DesktopEntryIndex
    {
        private readonly IReadOnlyList<string> _locales = DesktopEntry.GetLocaleCandidates(CultureInfo.CurrentUICulture);
        private readonly IReadOnlyList<string> _currentDesktops = GetCurrentDesktops();
        private readonly IconThemeResolver _icons;
        private readonly ConcurrentDictionary<string, string> _executables = new(StringComparer.Ordinal);

        public DesktopEntryIndex()
        {
            _icons = new IconThemeResolver(GetDataDirectories(), _currentDesktops);
        }

        /// <summary>
        /// $XDG_DATA_HOME followed by $XDG_DATA_DIRS (spec defaults when unset), most important first.
        /// Flatpak's export folders are part of XDG_DATA_DIRS when Flatpak is installed.
        /// </summary>
        public static List<string> GetDataDirectories()
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(dataHome) || !Path.IsPathRooted(dataHome))
            {
                dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            }

            var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            if (string.IsNullOrWhiteSpace(dataDirs))
            {
                dataDirs = "/usr/local/share:/usr/share";
            }

            // Relative entries are invalid per the Base Directory spec
            return dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries)
                .Where(Path.IsPathRooted)
                .Prepend(dataHome)
                .Select(d => d.Length > 1 ? d.TrimEnd('/') : d)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public static IEnumerable<string> GetApplicationDirectories()
        {
            return GetDataDirectories().Select(d => Path.Combine(d, "applications"));
        }

        private static IReadOnlyList<string> GetCurrentDesktops()
        {
            return (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty)
                .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        /// <summary>
        /// The program for a desktop file, or null if the entry shouldn't be shown in a launcher.
        /// </summary>
        public Win32 CreateProgram(string path)
        {
            DesktopEntry entry;
            try
            {
                entry = DesktopEntry.Load(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                ProgramLogger.LogDebug(nameof(DesktopEntryIndex), nameof(CreateProgram), path, $"Failed to read: {e.Message}");
                return null;
            }

            if (entry == null || !IsShown(entry))
                return null;

            var arguments = entry.GetExecArguments();
            var name = entry.GetString("Name");
            if (arguments.Count == 0 || name == null)
                return null;

            // Like GLib, hide entries whose program isn't installed
            var executable = FindExecutable(arguments[0]);
            if (executable == null)
                return null;

            var localizedName = entry.GetLocaleString("Name", _locales);
            var genericName = entry.GetLocaleString("GenericName", _locales);
            var description = genericName ?? entry.GetLocaleString("Comment", _locales) ?? string.Empty;

            // Description is only searched when shown, so GenericName also goes into the search terms
            var searchTerms = new[] { GetProgramName(arguments), GetDesktopIdName(path), genericName }
                .Concat(entry.GetLocaleStrings("Keywords", _locales))
                .Concat(entry.GetStrings("Keywords"))
                .Where(term => term != null &&
                               !term.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                               !term.Equals(localizedName, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new Win32
            {
                Name = name,
                LocalizedName = localizedName == name ? string.Empty : localizedName,
                ExecutableName = searchTerms.Length == 0 ? null : string.Join(' ', searchTerms),
                IcoPath = _icons.Resolve(entry.GetLocaleString("Icon", _locales)) ?? string.Empty,
                FullPath = path,
                LnkResolvedPath = executable,
                UniqueIdentifier = path,
                ParentDirectory = Path.GetDirectoryName(path),
                Description = description,
                Valid = true,
                Enabled = true
            };
        }

        private bool IsShown(DesktopEntry entry)
        {
            if (entry.GetString("Type") != "Application" || entry.GetBoolean("NoDisplay") || entry.GetBoolean("Hidden"))
                return false;

            var onlyShowIn = entry.GetStrings("OnlyShowIn");
            if (onlyShowIn.Count > 0 && !onlyShowIn.Any(d => _currentDesktops.Contains(d, StringComparer.OrdinalIgnoreCase)))
                return false;

            if (entry.GetStrings("NotShowIn").Any(d => _currentDesktops.Contains(d, StringComparer.OrdinalIgnoreCase)))
                return false;

            var tryExec = entry.GetString("TryExec");
            return tryExec == null || FindExecutable(tryExec) != null;
        }

        private string FindExecutable(string program)
        {
            return _executables.GetOrAdd(program, ExecutableLookup.Find);
        }

        /// <summary>
        /// File name of the launched program, skipping an "env VAR=value" prefix.
        /// </summary>
        private static string GetProgramName(IReadOnlyList<string> arguments)
        {
            var program = arguments[0];
            if (Path.GetFileName(program) == "env")
            {
                program = arguments.Skip(1).FirstOrDefault(a => !a.Contains('=') && !a.StartsWith('-')) ?? program;
            }

            return Path.GetFileName(program);
        }

        /// <summary>
        /// Last part of a reverse-DNS desktop file name, e.g. "Nautilus" for org.gnome.Nautilus.desktop
        /// whose Name is "Files".
        /// </summary>
        private static string GetDesktopIdName(string path)
        {
            var id = Path.GetFileNameWithoutExtension(path);
            var dot = id.LastIndexOf('.');
            return dot >= 0 && dot < id.Length - 1 ? id[(dot + 1)..] : id;
        }
    }

    /// <summary>
    /// PATH lookup. Distributions such as NixOS keep binaries outside /usr/bin, so nothing is hard-coded.
    /// </summary>
    [SupportedOSPlatform("linux")]
    internal static class ExecutableLookup
    {
        private const UnixFileMode ExecuteBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

        /// <summary>
        /// Full path of <paramref name="program"/> (a path, or a name searched on PATH), or null if it isn't an
        /// executable file.
        /// </summary>
        public static string Find(string program)
        {
            if (string.IsNullOrEmpty(program))
                return null;

            if (program.Contains('/'))
            {
                var fullPath = Path.GetFullPath(program);
                return IsExecutable(fullPath) ? fullPath : null;
            }

            var searchPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var directory in searchPath.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Path.IsPathRooted(directory))
                    continue;

                var candidate = Path.Combine(directory, program);
                if (IsExecutable(candidate))
                    return candidate;
            }

            return null;
        }

        private static bool IsExecutable(string path)
        {
            try
            {
                return File.Exists(path) && (File.GetUnixFileMode(path) & ExecuteBits) != 0;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
