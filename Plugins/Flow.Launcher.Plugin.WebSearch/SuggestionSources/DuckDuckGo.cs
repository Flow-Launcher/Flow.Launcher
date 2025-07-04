﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Text.Json;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    public class DuckDuckGo : SuggestionSource
    {
        private static readonly string ClassName = nameof(DuckDuckGo);

        public override async Task<List<string>> SuggestionsAsync(string query, CancellationToken token)
        {
            // When the search query is empty, DuckDuckGo returns `[]`. When it's not empty, it returns data
            // in the following format: `["query", ["suggestion1", "suggestion2", ...]]`.
            if (string.IsNullOrEmpty(query))
            {
                return new List<string>();
            }

            try
            {
                const string api = "https://duckduckgo.com/ac/?type=list&q=";

                await using var resultStream = await Main._context.API.HttpGetStreamAsync(api + Uri.EscapeDataString(query), token: token).ConfigureAwait(false);

                using var json = await JsonDocument.ParseAsync(resultStream, cancellationToken: token);

                var results = json.RootElement.EnumerateArray().ElementAt(1);

                return results.EnumerateArray().Select(o => o.GetString()).ToList();

            }
            catch (Exception e) when (e is HttpRequestException or {InnerException: TimeoutException})
            {
                Main._context.API.LogException(ClassName, "Can't get suggestion from DuckDuckGo", e);
                return null;
            }
            catch (JsonException e)
            {
                Main._context.API.LogException(ClassName, "Can't parse suggestions", e);
                return new List<string>();
            }
        }

        public override string ToString()
        {
            return "DuckDuckGo";
        }
    }
}
