using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Flow.Launcher.Plugin.Program.Logger;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program.Programs
{
    /// <summary>
    /// macOS program source: application bundles (*.app) are indexed as <see cref="Win32"/> entries so the
    /// plugin's cache, disabling and scoring work unchanged.
    /// </summary>
    public partial class Win32
    {
        private const string AppBundleExtension = "app";
        private const string OpenCommand = "/usr/bin/open";
        private const string PlutilCommand = "/usr/bin/plutil";

        // Don't skip entries flagged hidden: e.g. /Applications/Safari.app is a hidden symlink into the Safari cryptex
        private static readonly EnumerationOptions AppDirectoryEnumerationOptions = new()
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.None
        };

        private static readonly XmlReaderSettings PlistReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null
        };

        private partial void Launch(ActionContext c)
        {
            var info = new ProcessStartInfo(OpenCommand) { ArgumentList = { "-a", FullPath } };

            _ = Task.Run(() => Main.StartProcess(Process.Start, info));
        }

        private partial void OpenContainingFolder()
        {
            // Reveal the bundle in Finder
            var info = new ProcessStartInfo(OpenCommand) { ArgumentList = { "-R", FullPath } };

            _ = Task.Run(() => Main.StartProcess(Process.Start, info));
        }

        public partial List<Result> ContextMenus(IPublicAPI api)
        {
            return new List<Result>
            {
                new()
                {
                    Title = api.GetTranslation("flowlauncher_plugin_program_open_containing_folder"),
                    Action = _ =>
                    {
                        OpenContainingFolder();

                        return true;
                    },
                    IcoPath = "Images/folder.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe838"),
                },
            };
        }

        public static partial Win32[] All(Settings settings)
        {
            if (!OperatingSystem.IsMacOS())
            {
                Main.Context.API.LogWarn(nameof(Win32), "Program indexing is only supported on Windows and macOS");
                return Array.Empty<Win32>();
            }

            try
            {
                // Disabled custom sources are not in DisabledProgramSources
                var sources = GetApplicationDirectories()
                    .Select(directory => new ProgramSource(directory))
                    .Concat(settings.ProgramSources.Where(s => s.Enabled))
                    .Where(s => Directory.Exists(s.Location))
                    .Distinct();
                var commonParents = GetCommonParents(sources);

                var bundles = commonParents.AsParallel().SelectMany(EnumerateAppBundles);

                // Remove disabled programs in DisabledProgramSources
                return ExceptDisabledSource(bundles)
                    .AsParallel()
                    .Select(AppBundleProgram)
                    .Where(x => x.Valid)
                    .Distinct()
                    .ToArray();
            }
            catch (Exception e)
            {
                ProgramLogger.LogException("|Win32|All|Not available|An unexpected error occurred", e);

                return Array.Empty<Win32>();
            }
        }

        public static partial void WatchProgramUpdate(Settings settings)
        {
            if (!OperatingSystem.IsMacOS())
                return;

            var sources = GetApplicationDirectories()
                .Select(directory => new ProgramSource(directory))
                .Concat(settings.ProgramSources);

            string[] extensionsToWatch = [AppBundleExtension];
            foreach (var directory in from path in GetCommonParents(sources) where Directory.Exists(path) select path)
            {
                WatchDirectory(directory, extensionsToWatch);
            }

            _ = Task.Run(MonitorDirectoryChangeAsync);
        }

        private static IEnumerable<string> GetApplicationDirectories()
        {
            var userApplications = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications");

            return new[] { "/Applications", "/System/Applications", "/System/Applications/Utilities", userApplications };
        }

        /// <summary>
        /// Finds *.app bundles under <paramref name="directory"/>. Bundles are not descended into;
        /// other folders (e.g. /Applications/Utilities) are searched recursively.
        /// </summary>
        private static IEnumerable<string> EnumerateAppBundles(string directory)
        {
            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(directory));

            while (pending.Count > 0)
            {
                foreach (var subDirectory in EnumerateSubdirectories(pending.Pop()))
                {
                    if (subDirectory.Name.StartsWith('.'))
                    {
                        continue;
                    }

                    if (Extension(subDirectory.Name) == AppBundleExtension)
                    {
                        yield return subDirectory.FullName;
                    }
                    // Don't follow symbolic links to folders to avoid cycles
                    else if (!subDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        pending.Push(subDirectory);
                    }
                }
            }
        }

        private static DirectoryInfo[] EnumerateSubdirectories(DirectoryInfo directory)
        {
            try
            {
                return directory.GetDirectories("*", AppDirectoryEnumerationOptions);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                ProgramLogger.LogException($"|Win32|EnumerateSubdirectories|{directory.FullName}" +
                                           "|Failed to list the folder", e);

                return Array.Empty<DirectoryInfo>();
            }
        }

        private static Win32 AppBundleProgram(string path)
        {
            var bundleName = Path.GetFileNameWithoutExtension(path);
            var info = ReadInfoPlist(path, Path.Combine(path, "Contents", "Info.plist"));

            var name = GetNonEmptyValue(info, "CFBundleDisplayName") ?? GetNonEmptyValue(info, "CFBundleName") ?? bundleName;

            return new Win32
            {
                Name = name,
                // Bundle folder name (what Finder shows) stays searchable when the bundle name differs
                ExecutableName = name.Equals(bundleName, StringComparison.OrdinalIgnoreCase) ? null : bundleName,
                IcoPath = GetBundleIconPath(path, info) ?? path,
                FullPath = path,
                UniqueIdentifier = path,
                ParentDirectory = Path.GetDirectoryName(path),
                Description = string.Empty,
                Valid = true,
                Enabled = true
            };
        }

        private static string GetNonEmptyValue(Dictionary<string, string> info, string key)
        {
            return info.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        }

        /// <summary>
        /// Path of the bundle's .icns icon (CFBundleIconFile), or null if the bundle doesn't ship one.
        /// </summary>
        private static string GetBundleIconPath(string bundlePath, Dictionary<string, string> info)
        {
            var iconFile = GetNonEmptyValue(info, "CFBundleIconFile");
            if (iconFile == null)
                return null;

            var iconPath = Path.Combine(bundlePath, "Contents", "Resources", iconFile);
            if (File.Exists(iconPath))
                return iconPath;

            // CFBundleIconFile may omit the extension
            iconPath += ".icns";
            return File.Exists(iconPath) ? iconPath : null;
        }

        /// <summary>
        /// Reads the top-level string values of an Info.plist. XML plists are parsed directly;
        /// other formats (binary) and XML that fails to parse (e.g. stray NUL bytes) are converted with plutil.
        /// </summary>
        private static Dictionary<string, string> ReadInfoPlist(string bundlePath, string plistPath)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(plistPath))
                return values;

            Exception xmlError = null;
            try
            {
                if (IsXmlPlist(plistPath))
                {
                    using var reader = XmlReader.Create(plistPath, PlistReaderSettings);
                    ReadXmlPlist(reader, values);
                    return values;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or XmlException)
            {
                // CoreFoundation's parser is more lenient than XmlReader, so let plutil have a go
                xmlError = e;
                values.Clear();
            }

            try
            {
                ReadPlistWithPlutil(plistPath, values);
            }
            catch (Exception e) when (e is IOException or JsonException or Win32Exception or InvalidOperationException)
            {
                values.Clear();
                var xmlMessage = xmlError == null ? string.Empty : $"XML: {xmlError.Message}; ";
                ProgramLogger.LogDebug(nameof(Win32), nameof(ReadInfoPlist), bundlePath,
                    $"Failed to read Info.plist, falling back to the bundle name. {xmlMessage}plutil: {e.Message}");
            }

            return values;
        }

        private static bool IsXmlPlist(string plistPath)
        {
            using var stream = File.OpenRead(plistPath);
            int b;
            do
            {
                b = stream.ReadByte();
            } while (b is ' ' or '\t' or '\r' or '\n' or 0xEF or 0xBB or 0xBF); // whitespace and UTF-8 BOM

            return b == '<';
        }

        private static void ReadXmlPlist(XmlReader reader, Dictionary<string, string> values)
        {
            var document = new XmlDocument();
            document.Load(reader);

            var dict = document.DocumentElement?["dict"];
            if (dict == null)
                return;

            string key = null;
            foreach (XmlNode node in dict.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element)
                    continue;

                if (node.Name == "key")
                {
                    key = node.InnerText;
                    continue;
                }

                if (key != null && node.Name == "string")
                {
                    values[key] = node.InnerText;
                }

                key = null;
            }
        }

        private static void ReadPlistWithPlutil(string plistPath, Dictionary<string, string> values)
        {
            var info = new ProcessStartInfo(PlutilCommand)
            {
                ArgumentList = { "-convert", "json", "-o", "-", plistPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(info);
            var errorTask = process.StandardError.ReadToEndAsync();
            var json = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"plutil exited with code {process.ExitCode}: {errorTask.Result.Trim()}");
            }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    values[property.Name] = property.Value.GetString();
                }
            }
        }
    }
}
