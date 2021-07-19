// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Properties;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        private static IPublicAPI? _api;

        public static void Init(IPublicAPI api) => _api = api;

        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given list.
        /// </summary>
        /// <param name="list">The original result list to convert.</param>
        /// <param name="iconPath">The path to the icon of each entry.</param>
        /// <returns>A list with <see cref="Result"/>.</returns>
        internal static List<Result> GetResultList(
            in IEnumerable<WindowsSetting> list,
            Query query,
            string iconPath)
        {
            var resultList = new List<Result>();
            foreach (var entry in list)
            {
                var result = Predicate();
                if (result is null)
                    continue;

                AddOptionalToolTip(entry, result);

                resultList.Add(result);

                Result NewSettingResult(int score) => new Result()
                {
                    Action = _ => DoOpenSettingsAction(entry),
                    IcoPath = iconPath,
                    SubTitle = $"{Resources.Area} \"{entry.Area}\" {Resources.SubtitlePreposition} {entry.Type}",
                    Title = entry.Name,
                    ContextData = entry,
                    Score = score
                };

                Result? Predicate()
                {
                    Debug.Assert(_api != null, nameof(_api) + " != null");

                    var nameMatch = _api.FuzzySearch(query.Search, entry.Name);

                    if (nameMatch.IsSearchPrecisionScoreMet())
                    {
                        var settingResult = NewSettingResult(nameMatch.Score);
                        settingResult.TitleHighlightData = nameMatch.MatchData;
                        return settingResult;
                    }

                    var areaMatch = _api.FuzzySearch(query.Search, entry.Area);
                    if (areaMatch.IsSearchPrecisionScoreMet())
                    {
                        var settingResult = NewSettingResult(areaMatch.Score);
                        settingResult.SubTitleHighlightData = areaMatch.MatchData.Select(x => x + 6).ToList();
                        return settingResult;
                    }

                    return entry.AltNames?
                        .Select(altName => _api.FuzzySearch(query.Search, altName))
                        .Where(match => match.IsSearchPrecisionScoreMet())
                        .Select(altNameMatch => NewSettingResult(altNameMatch.Score))
                        .FirstOrDefault();

                }
            }

            return resultList;
        }



        /// <summary>
        /// Add a tool-tip to the given <see cref="Result"/>, based o the given <see cref="IWindowsSetting"/>.
        /// </summary>
        /// <param name="entry">The <see cref="WindowsSetting"/> that contain informations for the tool-tip.</param>
        /// <param name="result">The <see cref="Result"/> that need a tool-tip.</param>
        private static void AddOptionalToolTip(WindowsSetting entry, Result result)
        {
            var toolTipText = new StringBuilder();

            toolTipText.AppendLine($"{Resources.Application}: {entry.Type}");
            toolTipText.AppendLine($"{Resources.Area}: {entry.Area}");

            if (entry.AltNames != null && entry.AltNames.Any())
            {
                var altList = entry.AltNames.Aggregate((current, next) => $"{current}, {next}");

                toolTipText.AppendLine($"{Resources.AlternativeName}: {altList}");
            }

            toolTipText.Append($"{Resources.Command}: {entry.Command}");

            if (!string.IsNullOrEmpty(entry.Note))
            {
                toolTipText.AppendLine(string.Empty);
                toolTipText.AppendLine(string.Empty);
                toolTipText.Append($"{Resources.Note}: {entry.Note}");
            }

            result.TitleToolTip = toolTipText.ToString();
        }

        /// <summary>
        /// Open the settings page of the given <see cref="IWindowsSetting"/>.
        /// </summary>
        /// <param name="entry">The <see cref="WindowsSetting"/> that contain the information to open the setting on command level.</param>
        /// <returns><see langword="true"/> if the settings could be opened, otherwise <see langword="false"/>.</returns>
        private static bool DoOpenSettingsAction(WindowsSetting entry)
        {
            ProcessStartInfo processStartInfo;

            var command = entry.Command;

            if (command.Contains("%windir%", StringComparison.InvariantCultureIgnoreCase))
            {
                var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                command = command.Replace("%windir%", windowsFolder, StringComparison.InvariantCultureIgnoreCase);
            }

            if (command.Contains(' '))
            {
                var commandSplit = command.Split(' ');
                var file = commandSplit.First();
                var arguments = command[file.Length..].TrimStart();

                processStartInfo = new ProcessStartInfo(file, arguments)
                {
                    UseShellExecute = false,
                };
            }
            else
            {
                processStartInfo = new ProcessStartInfo(command)
                {
                    UseShellExecute = true,
                };
            }

            try
            {
                Process.Start(processStartInfo);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("can't open settings", exception, typeof(ResultHelper));
                return false;
            }
        }

        /// <summary>
        /// Set the score (known as order number or ranking number)
        /// for all <see cref="Results"/> in the given list, based on the given query.
        /// </summary>
        /// <param name="resultList">A list with <see cref="Result"/>s that need scores.</param>
        /// <param name="query">The query to calculated the score for the <see cref="Result"/>s.</param>
        private static void SetScores(IEnumerable<Result> resultList, string query)
        {
            var lowScore = 1_000;
            var mediumScore = 5_000;
            var highScore = 10_000;

            foreach (var result in resultList)
            {
                if (!(result.ContextData is WindowsSetting windowsSetting))
                {
                    continue;
                }

                if (windowsSetting.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
                {
                    result.Score = highScore--;
                    continue;
                }

                // If query starts with second or next word of name, set score.
                if (windowsSetting.Name.Contains($" {query}", StringComparison.CurrentCultureIgnoreCase))
                {
                    result.Score = mediumScore--;
                    continue;
                }

                if (windowsSetting.Area.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
                {
                    result.Score = lowScore--;
                    continue;
                }

                if (windowsSetting.AltNames is null)
                {
                    result.Score = lowScore--;
                    continue;
                }

                if (windowsSetting.AltNames.Any(x => x.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)))
                {
                    result.Score = mediumScore--;
                    continue;
                }

                result.Score = lowScore--;
            }
        }
    }
}
