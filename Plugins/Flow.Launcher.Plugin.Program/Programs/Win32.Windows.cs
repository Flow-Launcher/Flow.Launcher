using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Input;
using Flow.Launcher.Plugin.Program.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using IniParser;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.Program.Programs
{
    public partial class Win32
    {
        private const string ShortcutExtension = "lnk";
        private const string ExeExtension = "exe";

        private partial void Launch(ActionContext c)
        {
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
        }

        private partial void OpenContainingFolder()
        {
            Main.Context.API.OpenDirectory(ParentDirectory, FullPath);
        }

        public partial List<Result> ContextMenus(IPublicAPI api)
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

            return Extension(path) switch
            {
                ShortcutExtension => LnkProgram(path),
                ExeExtension => ExeProgram(path),
                UrlExtension => UrlProgram(path, protocols),
                _ => Win32Program(path)
            };
            ;
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


        public static partial Win32[] All(Settings settings)
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

                autoIndexPrograms = ProgramsHasher(autoIndexPrograms).ToArray();

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

        public static partial void WatchProgramUpdate(Settings settings)
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
    }
}
