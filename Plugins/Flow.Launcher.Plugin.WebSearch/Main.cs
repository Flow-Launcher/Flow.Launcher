﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using Microsoft.Web.WebView2.Core;

namespace Flow.Launcher.Plugin.WebSearch
{
    public class Main : IAsyncPlugin, ISettingProvider, IPluginI18n, IResultUpdated
    {
        private PluginInitContext _context;

        private Settings _settings;
        private SettingsViewModel _viewModel;

        internal const string Images = "Images";
        internal static string DefaultImagesDirectory;
        internal static string CustomImagesDirectory;

        private readonly int scoreStandard = 50;

        private readonly int scoreSuggestions = 48;

        private readonly string SearchSourceGlobalPluginWildCardSign = "*";


        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            if (FilesFolders.IsLocationPathString(query.Search))
                return new List<Result>();

            var searchSourceList = new List<SearchSource>();
            var results = new List<Result>();

            foreach (SearchSource searchSource in _settings.SearchSources.Where(o => (o.ActionKeyword == query.ActionKeyword ||
                                                                                      o.ActionKeyword == SearchSourceGlobalPluginWildCardSign)
                                                                                     && o.Enabled))
            {
                string keyword = string.Empty;
                keyword = searchSource.ActionKeyword == SearchSourceGlobalPluginWildCardSign ? query.ToString() : query.Search;
                var title = keyword;
                string subtitle = _context.API.GetTranslation("flowlauncher_plugin_websearch_search") + " " + searchSource.Title;

                // Action Keyword match apear on top
                var score = searchSource.ActionKeyword == SearchSourceGlobalPluginWildCardSign ? scoreStandard : scoreStandard + 1;

                // This populates the associated action keyword search entry
                if (string.IsNullOrEmpty(keyword))
                {
                    var result = new Result
                    {
                        Title = subtitle,
                        SubTitle = string.Empty,
                        IcoPath = searchSource.IconPath,
                        Score = score
                    };

                    results.Add(result);
                }
                else
                {
                    var result = new Result
                    {
                        Title = title,
                        SubTitle = subtitle,
                        IcoPath = searchSource.IconPath,
                        ActionKeywordAssigned = searchSource.ActionKeyword == SearchSourceGlobalPluginWildCardSign ? string.Empty : searchSource.ActionKeyword,
                        Score = score,
                        PreviewPanel = new Lazy<UserControl>(() => new PreviewPanel(searchSource.Url.Replace("{q}", Uri.EscapeDataString(keyword)))),
                        Action = c =>
                        {
                            _context.API.OpenUrl(searchSource.Url.Replace("{q}", Uri.EscapeDataString(keyword)));
                            return true;
                        }
                    };

                    results.Add(result);
                }

                ResultsUpdated?.Invoke(this, new ResultUpdatedEventArgs
                {
                    Results = results,
                    Query = query
                });

                await UpdateResultsFromSuggestionAsync(results, keyword, subtitle, searchSource, query, token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    return null;
            }

            return results;
        }

        private async Task UpdateResultsFromSuggestionAsync(List<Result> results, string keyword, string subtitle,
            SearchSource searchSource, Query query, CancellationToken token)
        {
            if (_settings.EnableSuggestion)
            {
                var suggestions = await SuggestionsAsync(keyword, subtitle, searchSource, token).ConfigureAwait(false);
                var enumerable = suggestions?.ToList();
                if (token.IsCancellationRequested || enumerable is not { Count: > 0 })
                    return;
                
                results.AddRange(enumerable);

                token.ThrowIfCancellationRequested();
            }
        }

        private async Task<IEnumerable<Result>> SuggestionsAsync(string keyword, string subtitle, SearchSource searchSource, CancellationToken token)
        {
            var source = _settings.SelectedSuggestion;
            if (source == null)
            {
                return new List<Result>();
            }
            //Suggestions appear below actual result, and appear above global action keyword match if non-global;
            var score = searchSource.ActionKeyword == SearchSourceGlobalPluginWildCardSign ? scoreSuggestions : scoreSuggestions + 1;

            var suggestions = await source.SuggestionsAsync(keyword, token).ConfigureAwait(false);

            token.ThrowIfCancellationRequested();

            var resultsFromSuggestion = suggestions?.Select(o => new Result
            {
                Title = o,
                SubTitle = subtitle,
                Score = score,
                IcoPath = searchSource.IconPath,
                ActionKeywordAssigned = searchSource.ActionKeyword == SearchSourceGlobalPluginWildCardSign ? string.Empty : searchSource.ActionKeyword,
                Action = c =>
                {
                    _context.API.OpenUrl(searchSource.Url.Replace("{q}", Uri.EscapeDataString(o)));

                    return true;
                }
            });
            return resultsFromSuggestion;
        }

        public Task InitAsync(PluginInitContext context)
        {
            return Task.Run(Init);

            async Task Init()
            {
                _context = context;

                _settings = _context.API.LoadSettingJsonStorage<Settings>();
                _viewModel = new SettingsViewModel(_settings);
                

                var pluginDirectory = _context.CurrentPluginMetadata.PluginDirectory;
                var bundledImagesDirectory = Path.Combine(pluginDirectory, Images);

                // Default images directory is in the WebSearch's application folder  
                DefaultImagesDirectory = Path.Combine(pluginDirectory, Images);
                Helper.ValidateDataDirectory(bundledImagesDirectory, DefaultImagesDirectory);

                // Custom images directory is in the WebSearch's data location folder 
                var name = Path.GetFileNameWithoutExtension(_context.CurrentPluginMetadata.ExecuteFileName);
                CustomImagesDirectory = Path.Combine(DataLocation.PluginSettingsDirectory, name, "CustomIcons");
            };
        }

        #region ISettingProvider Members

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_context, _viewModel);
        }

        #endregion

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_websearch_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("flowlauncher_plugin_websearch_plugin_description");
        }

        public event ResultUpdatedEventHandler ResultsUpdated;
    }
}
