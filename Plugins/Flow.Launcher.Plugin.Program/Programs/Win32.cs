using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.SharedModels;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Flow.Launcher.Plugin.Program.Views.Models;
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

        private const string UrlExtension = "url";
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
                        OpenContainingFolder();
                        return true;
                    }

                    Launch(c);

                    return true;
                }
            };

            return result;
        }


        public override string ToString()
        {
            return Name;
        }

        public partial List<Result> ContextMenus(IPublicAPI api);

        public static partial Win32[] All(Settings settings);

        public static partial void WatchProgramUpdate(Settings settings);

        private partial void Launch(ActionContext c);

        private partial void OpenContainingFolder();

        private static readonly List<FileSystemWatcher> Watchers = new();

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

        private static readonly Channel<byte> indexQueue = Channel.CreateBounded<byte>(1);

        public static async Task MonitorDirectoryChangeAsync()
        {
            var reader = indexQueue.Reader;
            while (await reader.WaitToReadAsync())
            {
                await Task.Delay(500);
                while (reader.TryRead(out _))
                {
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
