using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Win32;
using Flow.Launcher.Plugin.Program.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.SharedModels;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Flow.Launcher.Plugin.Program.Views.Models;
using IniParser;
using System.Windows.Input;
using MemoryPack;

namespace Flow.Launcher.Plugin.Program.Programs
{
    [MemoryPackable]
    public partial class Win32 : IProgram, IEquatable<Win32>
    {
        public string Name { get; set; }

        public string UniqueIdentifier
        {
            get => _uid;
            set => _uid = value == null ? string.Empty : value.ToLowerInvariant();
        } // For path comparison

        public string IcoPath { get; set; }

        /// <summary>
        /// Path of the file. It's the path of .lnk and .url for .lnk and .url files.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Path of the executable for .lnk, or the URL for .url
        /// </summary>
        public string LnkResolvedPath { get; set; }

        /// <summary>
        /// Path of the actual executable file
        /// </summary>
        public string ExecutablePath => LnkResolvedPath ?? FullPath;

        /// <summary>
        /// Arguments for the executable.
        /// </summary>
        public string Args { get; set; }

        public string ParentDirectory { get; set; }

        /// <summary>
        /// Name of the executable for .lnk files
        /// </summary>
        public string ExecutableName { get; set; }

        public string Description { get; set; }
        public bool Valid { get; set; }
        public bool Enabled { get; set; }
        public string Location => ParentDirectory;

        // Localized name based on windows display language
        public string LocalizedName { get; set; } = string.Empty;

        // File timestamp when this entry was parsed (see Stamp()). Must stay the last serialized member:
        // MemoryPack only tolerates members added at the end; old caches load it as default.
        public DateTime ParsedStamp { get; set; }
        public DateTime ParsedTargetStamp { get; set; } // .lnk target's timestamp at parse time; same rules as above

        private const string ShortcutExtension = "lnk";
        private const string UrlExtension = "url";
        private const string ExeExtension = "exe";
        private string _uid = string.Empty;

        private static readonly Win32 Default = new()
        {
            Name = string.Empty,
            Description = string.Empty,
            IcoPath = string.Empty,
            FullPath = string.Empty,
            LnkResolvedPath = null,
            ParentDirectory = string.Empty,
            ExecutableName = null,
            UniqueIdentifier = string.Empty,
            Valid = false,
            Enabled = false
        };

        private static MatchResult Match(string query, IReadOnlyCollection<string> candidates)
        {
            if (candidates.Count == 0)
                return null;

            var match = candidates.Select(candidate => Main.Context.API.FuzzySearch(query, candidate))
                .MaxBy(match => match.Score);

            return match?.IsSearchPrecisionScoreMet() ?? false ? match : null;
        }

        public Result Result(string query, IPublicAPI api)
        {
            string title;
            MatchResult matchResult;

            // Name of the result
            // Check equality to avoid matching again in candidates
            bool useLocalizedName = !string.IsNullOrEmpty(LocalizedName) && !Name.Equals(LocalizedName);
            string resultName = useLocalizedName ? LocalizedName : Name;

            if (!Main._settings.EnableDescription || string.IsNullOrWhiteSpace(Description) ||
                resultName.Equals(Description))
            {
                title = resultName;
                matchResult = Main.Context.API.FuzzySearch(query, resultName);
            }
            else
            {
                // Search in both
                title = $"{resultName}: {Description}";
                var nameMatch = Main.Context.API.FuzzySearch(query, resultName);
                var descriptionMatch = Main.Context.API.FuzzySearch(query, Description);
                if (descriptionMatch.Score > nameMatch.Score)
                {
                    for (int i = 0; i < descriptionMatch.MatchData.Count; i++)
                    {
                        descriptionMatch.MatchData[i] += resultName.Length + 2; // 2 is ": "
                    }

                    matchResult = descriptionMatch;
                }
                else
                {
                    matchResult = nameMatch;
                }
            }

            List<string> candidates = new List<string>();

            if (!matchResult.IsSearchPrecisionScoreMet() && !string.IsNullOrEmpty(query))
            {
                if (ExecutableName != null) // only lnk program will need this one
                {
                    candidates.Add(ExecutableName);
                }

                if (useLocalizedName)
                {
                    candidates.Add(Name);
                }

                matchResult = Match(query, candidates);
                if (matchResult == null)
                {
                    return null;
                }
                else
                {
                    // Nothing to highlight in title in this case
                    matchResult.MatchData.Clear();
                }
            }

            string subtitle = string.Empty;
            if (!Main._settings.HideAppsPath)
            {
                if (Extension(FullPath) == UrlExtension)
                {
                    subtitle = LnkResolvedPath;
                }
                else
                {
                    subtitle = FullPath;
                }
            }

            var result = new Result
            {
                Title = title,
                AutoCompleteText = resultName,
                SubTitle = subtitle,
                IcoPath = IcoPath,
                Score = matchResult.Score,
                TitleHighlightData = matchResult.MatchData,
                ContextData = this,
                TitleToolTip = $"{title}\n{ExecutablePath}",
                Action = c =>
                {
                    // Ctrl + Enter to open containing folder
                    bool openFolder = c.SpecialKeyState.ToModifierKeys() == ModifierKeys.Control;
                    if (openFolder)
                    {
                        Main.Context.API.OpenDirectory(ParentDirectory, FullPath);
                        return true;
                    }

                    // Ctrl + Shift + Enter to run as admin
                    bool runAsAdmin = c.SpecialKeyState.ToModifierKeys() == (ModifierKeys.Control | ModifierKeys.Shift);

                    var info = new ProcessStartInfo
                    {
                        FileName = FullPath,
                        WorkingDirectory = ParentDirectory,
                        UseShellExecute = true,
                        Verb = runAsAdmin ? "runas" : "",
                    };

                    _ = Task.Run(() => Main.StartProcess(Process.Start, info));

                    return true;
                }
            };

            return result;
        }


        public List<Result> ContextMenus(IPublicAPI api)
        {
            var contextMenus = new List<Result>
            {
                new()
                {
                    Title = api.GetTranslation("flowlauncher_plugin_program_run_as_different_user"),
                    Action = c =>
                    {
                        var info = new ProcessStartInfo
                        {
                            FileName = FullPath, WorkingDirectory = ParentDirectory, UseShellExecute = true
                        };

                        _ = Task.Run(() => Main.StartProcess(ShellCommand.RunAsDifferentUser, info));

                        return true;
                    },
                    IcoPath = "Images/user.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ee"),
                },
                new()
                {
                    Title = api.GetTranslation("flowlauncher_plugin_program_run_as_administrator"),
                    Action = c =>
                    {
                        var info = new ProcessStartInfo
                        {
                            FileName = FullPath,
                            WorkingDirectory = ParentDirectory,
                            Verb = "runas",
                            UseShellExecute = true
                        };

                        _ = Task.Run(() => Main.StartProcess(Process.Start, info));

                        return true;
                    },
                    IcoPath = "Images/cmd.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ef"),
                },
                new()
                {
                    Title = api.GetTranslation("flowlauncher_plugin_program_open_containing_folder"),
                    Action = _ =>
                    {
                        Main.Context.API.OpenDirectory(ParentDirectory, FullPath);

                        return true;
                    },
                    IcoPath = "Images/folder.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe838"),
                },
            };
            if (Extension(FullPath) == ShortcutExtension)
            {
                contextMenus.Add(OpenTargetFolderContextMenuResult(api));
            }
            return contextMenus;
        }

        private Result OpenTargetFolderContextMenuResult(IPublicAPI api)
        {
            return new Result
            {
                Title = api.GetTranslation("flowlauncher_plugin_program_open_target_folder"),
                Action = _ =>
                {
                    api.OpenDirectory(Path.GetDirectoryName(ExecutablePath), ExecutablePath);
                    return true;
                },
                IcoPath = "Images/folder.png",
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe8de"),
            };
        }

        public override string ToString()
        {
            return Name;
        }

        private static readonly List<FileSystemWatcher> Watchers = new();

        private static Win32 Win32Program(string path)
        {
            try
            {
                var p = new Win32
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    IcoPath = path,
                    FullPath = path,
                    UniqueIdentifier = path,
                    ParentDirectory = Directory.GetParent(path).FullName,
                    Description = string.Empty,
                    Valid = true,
                    Enabled = true
                };
                return p;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException($"|Win32|Win32Program|{path}" +
                                           $"|Permission denied when trying to load the program from {path}", e);

                return Default;
            }
#if !DEBUG
            catch (Exception e)
            {
                ProgramLogger.LogException($"|Win32|Win32Program|{path}" +
                                                "|An unexpected error occurred in the calling method Win32Program", e);

                return Default;
            }
#endif
        }

        private static Win32 LnkProgram(string path)
        {
            var program = Win32Program(path);
            try
            {   
                var shellLink = ShellLinkReader.Read(path);
                string target = shellLink.TargetPath;

                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                {
                    program.LnkResolvedPath = Path.GetFullPath(target);
                    program.ExecutableName = Path.GetFileNameWithoutExtension(target);

                    var args = shellLink.Arguments;
                    if (!string.IsNullOrEmpty(args))
                    {
                        program.Args = args;
                    }

                    var description = shellLink.Description;
                    if (!string.IsNullOrEmpty(description))
                    {
                        program.Description = description;
                    }
                    else
                    {
                        var info = FileVersionInfo.GetVersionInfo(target);
                        if (!string.IsNullOrEmpty(info.FileDescription))
                        {
                            program.Description = info.FileDescription;
                        }
                    }
                }

                program.LocalizedName = ShellLocalization.GetLocalizedName(path);

                return program;
            }
            catch (FileNotFoundException e)
            {
                ProgramLogger.LogException($"|Win32|LnkProgram|{path}" +
                                           "|An unexpected error occurred in the calling method LnkProgram", e);

                return Default;
            }
#if !DEBUG //Only do a catch all in production. This is so make developer aware of any unhandled exception and add the exception handling in.
            catch (Exception e)
            {
                ProgramLogger.LogException($"|Win32|LnkProgram|{path}" +
                                                "|An unexpected error occurred in the calling method LnkProgram", e);

                return Default;
            }
#endif
        }

        private static Win32 UrlProgram(string path, string[] protocols)
        {
            var program = Win32Program(path);
            program.Valid = false;

            try
            {
                var parser = new FileIniDataParser();
                var data = parser.ReadFile(path);
                var urlSection = data["InternetShortcut"];
                var url = urlSection?["URL"];
                if (string.IsNullOrEmpty(url))
                {
                    return program;
                }

                foreach (var protocol in protocols)
                {
                    if (url.StartsWith(protocol))
                    {
                        program.LnkResolvedPath = url;
                        program.Valid = true;
                        break;
                    }
                }

                var iconPath = urlSection?["IconFile"];
                if (!string.IsNullOrEmpty(iconPath))
                {
                    program.IcoPath = iconPath;
                }
            }
            catch (Exception)
            {
                // Many files do not have the required fields, so no logging is done.
            }

            return program;
        }

        private static Win32 ExeProgram(string path)
        {
            try
            {
                var program = Win32Program(path);
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(info.FileDescription))
                    program.Description = info.FileDescription;
                return program;
            }
            catch (FileNotFoundException e)
            {
                ProgramLogger.LogException($"|Win32|ExeProgram|{path}" +
                                           $"|File not found when trying to load the program from {path}", e);

                return Default;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException($"|Win32|ExeProgram|{path}" +
                                           $"|Permission denied when trying to load the program from {path}", e);

                return Default;
            }
        }

        private static IEnumerable<string> EnumerateProgramsInDir(string directory, string[] suffixes,
            bool recursive = true)
        {
            if (!Directory.Exists(directory))
                return Enumerable.Empty<string>();

            return Directory.EnumerateFiles(
                    directory, "*",
                    new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = recursive })
                .Where(x => suffixes.Contains(Extension(x)));
        }

        private static string Extension(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(extension))
            {
                return extension[1..]; // remove dot
            }
            else
            {
                return string.Empty;
            }
        }

        private static IEnumerable<Win32> UnregisteredPrograms(List<string> directories, string[] suffixes,
            string[] protocols)
        {
            // Disabled custom sources are not in DisabledProgramSources
            var paths = directories.AsParallel()
                .SelectMany(s => EnumerateProgramsInDir(s, suffixes));

            // Remove disabled programs in DisabledProgramSources
            var programs = ExceptDisabledSource(paths).Select(x => GetProgramFromPath(x, protocols));
            return programs;
        }

        private static IEnumerable<Win32> StartMenuPrograms(string[] suffixes, string[] protocols)
        {
            var allPrograms = GetStartMenuPaths()
                .SelectMany(p => EnumerateProgramsInDir(p, suffixes))
                .Distinct();

            var startupPaths = GetStartupPaths();

            var programs = ExceptDisabledSource(allPrograms)
                .Where(x => !startupPaths.Any(startup => FilesFolders.PathContains(startup, x)))
                .Select(x => GetProgramFromPath(x, protocols));
            return programs;
        }

        private static IEnumerable<Win32> PATHPrograms(string[] suffixes, string[] protocols,
            List<string> commonParents)
        {
            var pathEnv = Environment.GetEnvironmentVariable("Path");
            if (String.IsNullOrEmpty(pathEnv))
            {
                return Array.Empty<Win32>();
            }

            var paths = pathEnv.Split(";", StringSplitOptions.RemoveEmptyEntries).DistinctBy(p => p.ToLowerInvariant());

            var toFilter = paths.Where(x => commonParents.All(parent => !FilesFolders.PathContains(parent, x)))
                .AsParallel()
                .SelectMany(p => EnumerateProgramsInDir(p, suffixes, recursive: false));

            var programs = ExceptDisabledSource(toFilter.Distinct())
                .Select(x => GetProgramFromPath(x, protocols));
            return programs;
        }

        private static IEnumerable<Win32> AppPathsPrograms(string[] suffixes, string[] protocols)
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ee872121
            const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

            IEnumerable<string> toFilter = Enumerable.Empty<string>();

            using var rootMachine = Registry.LocalMachine.OpenSubKey(appPaths);
            using var rootUser = Registry.CurrentUser.OpenSubKey(appPaths);

            if (rootMachine != null)
            {
                toFilter = toFilter.Concat(GetPathFromRegistry(rootMachine));
            }

            if (rootUser != null)
            {
                toFilter = toFilter.Concat(GetPathFromRegistry(rootUser));
            }

            toFilter = toFilter.Distinct().Where(p => suffixes.Contains(Extension(p)));

            var programs = ExceptDisabledSource(toFilter)
                .Select(x => GetProgramFromPath(x, protocols)).Where(x => x.Valid)
                .ToList(); // ToList due to disposing issue
            return programs;
        }

        private static IEnumerable<string> GetPathFromRegistry(RegistryKey root)
        {
            return root
                .GetSubKeyNames()
                .Select(x => GetProgramPathFromRegistrySubKeys(root, x))
                .Distinct();
        }

        private static string GetProgramPathFromRegistrySubKeys(RegistryKey root, string subKey)
        {
            var path = string.Empty;
            try
            {
                using (var key = root.OpenSubKey(subKey))
                {
                    if (key == null)
                        return string.Empty;

                    var defaultValue = string.Empty;
                    path = key.GetValue(defaultValue) as string;
                }

                if (string.IsNullOrEmpty(path))
                    return string.Empty;

                // fix path like this: ""\"C:\\folder\\executable.exe\""
                return path = path.Trim('"', ' ');
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                ProgramLogger.LogException($"|Win32|GetProgramPathFromRegistrySubKeys|{path}" +
                                           $"|Permission denied when trying to load the program from {path}", e);

                return string.Empty;
            }
        }

        private static Win32 GetProgramFromPath(string path, string[] protocols)
        {
            if (string.IsNullOrEmpty(path))
                return Default;

            path = Environment.ExpandEnvironmentVariables(path);

            DateTime? lastWrite = null;
            try { lastWrite = Stamp(path); } catch { /* odd registry path: just don't memoize */ }

            if (lastWrite is { } t && ParsedPrograms.TryGetValue(path, out var cached) && cached.ParsedStamp == t &&
                cached.ParsedTargetStamp == TargetStamp(cached))
                return cached;

            var program = Extension(path) switch
            {
                ShortcutExtension => LnkProgram(path),
                ExeExtension => ExeProgram(path),
                UrlExtension => UrlProgram(path, protocols),
                _ => Win32Program(path)
            };

            if (lastWrite is { } w && !ReferenceEquals(program, Default)) // Default is shared: never stamp it
            {
                program.ParsedStamp = w;
                program.ParsedTargetStamp = TargetStamp(program);
                ParsedPrograms[path] = program;
            }
            return program;
        }

        // Parsing a .lnk/.exe (shell link, FileVersionInfo, shell localization) is the expensive part of indexing.
        // Reindexes reuse entries whose file (and, for .lnk, target) timestamps are exactly those recorded at parse time.
        private static readonly ConcurrentDictionary<string, Win32> ParsedPrograms =
            new(StringComparer.OrdinalIgnoreCase);

        public static void ClearParsedPrograms() => ParsedPrograms.Clear();

        // Compared for exact equality, so a replaced file is caught even if its mtime is older.
        // Not max(write, creation): overwriting in place keeps the creation time, which would hide the change.
        private static DateTime Stamp(string path) => File.GetLastWriteTimeUtc(path);

        // A .lnk's description may come from its target exe, so the target's timestamp matters too.
        // Missing target -> 1601 stamp, so a target that disappears also forces a reparse.
        private static DateTime TargetStamp(Win32 program)
        {
            if (program.LnkResolvedPath == null || Extension(program.FullPath) != ShortcutExtension)
                return default;
            try
            {
                return Stamp(program.LnkResolvedPath);
            }
            catch
            {
                return default; // odd path: fall back to checking only the .lnk itself
            }
        }

        // Seed from the on-disk index so the first reindex after a restart is incremental too.
        // Entries from caches written before ParsedStamp existed have it as default and are parsed again once.
        public static void SeedParsedPrograms(IEnumerable<Win32> cached)
        {
            foreach (var program in cached)
            {
                if (string.IsNullOrEmpty(program.FullPath) || program.ParsedStamp == default)
                    continue;
                try
                {
                    if (Stamp(program.FullPath) == program.ParsedStamp &&
                        TargetStamp(program) == program.ParsedTargetStamp)
                        ParsedPrograms.TryAdd(program.FullPath, program);
                }
                catch
                {
                    // unreadable path: it'll just be parsed again
                }
            }
        }

        public static IEnumerable<string> ExceptDisabledSource(IEnumerable<string> paths)
        {
            return ExceptDisabledSource(paths, x => x.ToLowerInvariant());
        }

        public static IEnumerable<TSource> ExceptDisabledSource<TSource>(IEnumerable<TSource> sources,
            Func<TSource, string> keySelector)
        {
            return Main._settings.DisabledProgramSources.Count == 0
                ? sources
                : ExceptDisabledSourceEnumerable(sources, keySelector);

            static IEnumerable<TSource> ExceptDisabledSourceEnumerable(IEnumerable<TSource> elements,
                Func<TSource, string> selector)
            {
                var set = Main._settings.DisabledProgramSources.Select(x => x.UniqueIdentifier).ToHashSet();

                foreach (var element in elements)
                {
                    if (!set.Contains(selector(element)))
                        yield return element;
                }
            }
        }

        public static IEnumerable<T> DistinctBy<T, R>(IEnumerable<T> source, Func<T, R> selector)
        {
            var set = new HashSet<R>();
            foreach (var item in source)
            {
                if (set.Add(selector(item)))
                    yield return item;
            }
        }

        private static IEnumerable<Win32> ProgramsHasher(IEnumerable<Win32> programs)
        {
            var startMenuPaths = GetStartMenuPaths();
            return programs.GroupBy(p => (p.ExecutablePath + p.Args).ToLowerInvariant())
                .AsParallel()
                .SelectMany(g =>
                {
                    // is shortcut and in start menu
                    var startMenu = g.Where(g =>
                            g.LnkResolvedPath != null &&
                            startMenuPaths.Any(x => FilesFolders.PathContains(x, g.FullPath)))
                        .ToList();
                    if (startMenu.Any())
                        return startMenu.Take(1);

                    // distinct by description
                    var temp = g.Where(g => !string.IsNullOrEmpty(g.Description)).ToList();
                    if (temp.Any())
                        return temp.Take(1);
                    return g.Take(1);
                });
        }


        public static Win32[] All(Settings settings)
        {
            try
            {
                var programs = Enumerable.Empty<Win32>();
                var suffixes = settings.GetSuffixes();
                var protocols = settings.GetProtocols();

                // Disabled custom sources are not in DisabledProgramSources
                var sources = settings.ProgramSources.Where(s => Directory.Exists(s.Location) && s.Enabled).Distinct();
                var commonParents = GetCommonParents(sources);

                var unregistered = UnregisteredPrograms(commonParents, suffixes, protocols);

                programs = programs.Concat(unregistered);

                var autoIndexPrograms = Enumerable.Empty<Win32>(); // for single programs, not folders

                if (settings.EnableRegistrySource)
                {
                    var appPaths = AppPathsPrograms(suffixes, protocols);
                    autoIndexPrograms = autoIndexPrograms.Concat(appPaths);
                }

                if (settings.EnableStartMenuSource)
                {
                    var startMenu = StartMenuPrograms(suffixes, protocols);
                    autoIndexPrograms = autoIndexPrograms.Concat(startMenu);
                }

                if (settings.EnablePathSource)
                {
                    var path = PATHPrograms(settings.GetSuffixes(), protocols, commonParents);
                    programs = programs.Concat(path);
                }

                // Materialize first: parsing runs on the calling (below-normal) thread, not on PLINQ pool threads.
                autoIndexPrograms = ProgramsHasher(autoIndexPrograms.ToArray()).ToArray();

                return programs.Concat(autoIndexPrograms).Where(x => x.Valid).Distinct().ToArray();
            }
#if DEBUG //This is to make developer aware of any unhandled exception and add in handling.
            catch (Exception)
            {
                throw;
            }
#endif

#if !DEBUG //Only do a catch all in production.
            catch (Exception e)
            {
                ProgramLogger.LogException("|Win32|All|Not available|An unexpected error occurred", e);

                return Array.Empty<Win32>();
            }
#endif
        }

        public override int GetHashCode()
        {
            return UniqueIdentifier.GetHashCode();
        }

        public bool Equals([AllowNull] Win32 other)
        {
            if (other == null)
                return false;

            return UniqueIdentifier == other.UniqueIdentifier;
        }

        public override bool Equals(object obj)
        {
            if (obj is Win32 other)
            {
                return UniqueIdentifier == other.UniqueIdentifier;
            }
            else
            {
                return false;
            }
        }

        private static IEnumerable<string> GetStartMenuPaths()
        {
            var userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);

            return new[] { userStartMenu, commonStartMenu };
        }

        private static IEnumerable<string> GetStartupPaths()
        {
            var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

            return new[] { userStartup, commonStartup };
        }

        public static void WatchProgramUpdate(Settings settings)
        {
            var paths = new List<string>();
            if (settings.EnableStartMenuSource)
                paths.AddRange(GetStartMenuPaths());

            var customSources = GetCommonParents(settings.ProgramSources);
            paths.AddRange(customSources);

            var fileExtensionToWatch = settings.GetSuffixes();
            foreach (var directory in from path in paths where Directory.Exists(path) select path)
            {
                WatchDirectory(directory, fileExtensionToWatch);
            }

            _ = Task.Run(MonitorDirectoryChangeAsync);
        }

        private static readonly Channel<byte> indexQueue = Channel.CreateBounded<byte>(1);

        public static async Task MonitorDirectoryChangeAsync()
        {
            var reader = indexQueue.Reader;
            while (await reader.WaitToReadAsync())
            {
                // Installers add/remove shortcuts in bursts over several seconds: wait for 3s of quiet, index once.
                while (true)
                {
                    while (reader.TryRead(out _))
                    {
                    }

                    var quiet = Task.Delay(3000);
                    if (await Task.WhenAny(reader.WaitToReadAsync().AsTask(), quiet).ConfigureAwait(false) == quiet)
                        break;
                }

                await Main.IndexWin32ProgramsAsync(resetCache: true).ConfigureAwait(false);
            }
        }

        public static void WatchDirectory(string directory, string[] extensions)
        {
            if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Path Not Exist");
            }

            var watcher = new FileSystemWatcher(directory);

            watcher.Created += static (_, _) => indexQueue.Writer.TryWrite(default);
            watcher.Deleted += static (_, _) => indexQueue.Writer.TryWrite(default);
            watcher.EnableRaisingEvents = true;
            watcher.IncludeSubdirectories = true;
            foreach (var extension in extensions)
            {
                watcher.Filters.Add($"*.{extension}");
            }

            Watchers.Add(watcher);
        }

        public static void Dispose()
        {
            foreach (var fileSystemWatcher in Watchers)
            {
                fileSystemWatcher.Dispose();
            }
        }

        private static List<string> GetCommonParents(IEnumerable<ProgramSource> programSources)
        {
            // To avoid unnecessary io
            // like c:\windows and c:\windows\system32
            var grouped = programSources.GroupBy(p => p.Location.ToLowerInvariant()[0]); // group by disk
            List<string> result = new();
            foreach (var group in grouped)
            {
                HashSet<ProgramSource> parents = group.ToHashSet();
                foreach (var source in group)
                {
                    if (parents.Any(p => FilesFolders.PathContains(p.Location, source.Location)))
                    {
                        parents.Remove(source);
                    }
                }

                result.AddRange(parents.Select(x => x.Location));
            }

            return result.DistinctBy(x => x.ToLowerInvariant()).ToList();
        }
    }
}
