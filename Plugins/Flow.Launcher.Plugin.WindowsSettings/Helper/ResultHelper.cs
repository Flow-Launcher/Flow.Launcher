using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Flow.Launcher.Plugin.WindowsSettings.Classes;
using Flow.Launcher.Plugin.WindowsSettings.Properties;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        private static IPublicAPI? _api;

        public static void Init(IPublicAPI api) => _api = api;

        private static List<Result> GetDefaultResults(in IEnumerable<WindowsSetting> list,
            string windowsSettingIconPath,
            string controlPanelIconPath)
        {
            return list.Select(entry =>
            {
                var result = NewSettingResult(100, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry);
                AddOptionalToolTip(entry, result);
                return result;
            }).ToList();
        }

        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given list.
        /// </summary>
        /// <param name="list">The original result list to convert.</param>
        /// <param name="query">Query for specific result List</param>
        /// <param name="windowsSettingIconPath">The path to the icon of each entry.</param>
        /// <returns>A list with <see cref="Result"/>.</returns>
        internal static List<Result> GetResultList(
            in IEnumerable<WindowsSetting> list,
            Query query,
            string windowsSettingIconPath,
            string controlPanelIconPath)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return GetDefaultResults(list, windowsSettingIconPath, controlPanelIconPath);
            }

            var resultList = new List<Result>();

            foreach (var entry in list)
            {
                // Adjust the score to lower the order of many irrelevant matches from area strings
                // that may only be for description.
                const int nonNameMatchScoreAdj = 10;


                Result? result;
                Debug.Assert(_api != null, nameof(_api) + " != null");

                var nameMatch = _api.FuzzySearch(query.Search, entry.Name);

                if (nameMatch.IsSearchPrecisionScoreMet())
                {
                    var settingResult = NewSettingResult(nameMatch.Score, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry);
                    settingResult.TitleHighlightData = nameMatch.MatchData;
                    result = settingResult;
                }
                else
                {
                    var areaMatch = _api.FuzzySearch(query.Search, entry.Area);
                    if (areaMatch.IsSearchPrecisionScoreMet())
                    {
                        var settingResult = NewSettingResult(areaMatch.Score - nonNameMatchScoreAdj, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry);
                        result = settingResult;
                    }
                    else
                    {
                        result = entry.AltNames?
                            .Select(altName => _api.FuzzySearch(query.Search, altName))
                            .Where(match => match.IsSearchPrecisionScoreMet())
                            .Select(altNameMatch => NewSettingResult(altNameMatch.Score - nonNameMatchScoreAdj, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry))
                            .FirstOrDefault();
                    }

                    if (result is null && entry.Keywords is not null)
                    {
                        string[] searchKeywords = query.SearchTerms;

                        if (searchKeywords
                            .All(x => entry
                                .Keywords
                                .SelectMany(x => x)
                                .Contains(x, StringComparer.CurrentCultureIgnoreCase))
                        )
                            result = NewSettingResult(nonNameMatchScoreAdj, entry.Type, windowsSettingIconPath, controlPanelIconPath, entry);
                    }
                }

                if (result is null)
                    continue;

                AddOptionalToolTip(entry, result);

                resultList.Add(result);
            }

            return resultList;
        }

        private const int TaskLinkScorePenalty = 50;

        private static Result NewSettingResult(int score, string type, string windowsSettingIconPath, string controlPanelIconPath, WindowsSetting entry) => new()
        {
            Action = _ => DoOpenSettingsAction(entry),
            IcoPath = type == "AppSettingsApp" ? windowsSettingIconPath : controlPanelIconPath,
            Glyph = entry.IconGlyph,
            SubTitle = GetSubtitle(entry.Area, type),
            Title = entry.Name,
            ContextData = entry,
            Score = score - (type == "TaskLink" ? TaskLinkScorePenalty : 0),
        };

        private static string GetSubtitle(string section, string entryType)
        {
            var settingType = entryType == "AppSettingsApp" ? Resources.AppSettingsApp : Resources.AppControlPanel;

            return $"{settingType} > {section}";
        }

        /// <summary>
        /// Add a tool-tip to the given <see cref="Result"/>, based o the given <see cref="IWindowsSetting"/>.
        /// </summary>
        /// <param name="entry">The <see cref="WindowsSetting"/> that contain informations for the tool-tip.</param>
        /// <param name="result">The <see cref="Result"/> that need a tool-tip.</param>
        private static void AddOptionalToolTip(WindowsSetting entry, Result result)
        {
            var toolTipText = new StringBuilder();

            var settingType = entry.Type == "AppSettingsApp" ? Resources.AppSettingsApp : Resources.AppControlPanel;

            toolTipText.AppendLine($"{Resources.Application}: {settingType}");
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
            result.SubTitleToolTip = result.TitleToolTip;
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

            command = Environment.ExpandEnvironmentVariables(command);

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
            catch (Win32Exception)
            {
                try
                {
                    processStartInfo.UseShellExecute = true;
                    processStartInfo.Verb = "runas";
                    Process.Start(processStartInfo);
                    return true;
                }
                catch (Exception exception)
                {
                    Log.Exception("can't open settings on elevated permission", exception, typeof(ResultHelper));
                    return false;
                }
            }
            catch (Exception exception)
            {
                Log.Exception("can't open settings", exception, typeof(ResultHelper));
                return false;
            }
        }
    }
}
