using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System.Net.Http;
using System.Text.Json;
using System.IO;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    public class Google : SuggestionSource
    {
        public override async Task<List<string>> Suggestions(string query)
        {
            Stream resultStream;
            try
            {
                const string api = "https://www.google.com/complete/search?output=chrome&q=";
                resultStream = await Http.GetStreamAsync(api + Uri.EscapeUriString(query)).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                Log.Exception("|Google.Suggestions|Can't get suggestion from google", e);
                return new List<string>();
            }
            if (resultStream.Length == 0) return new List<string>();
            JsonDocument json;
            try
            {
                json = await JsonDocument.ParseAsync(resultStream);
            }
            catch (JsonException e)
            {
                Log.Exception("|Google.Suggestions|can't parse suggestions", e);
                return new List<string>();
            }

            var results = json?.RootElement.EnumerateArray().ElementAt(1);

            return results?.EnumerateArray().Select(o => o.GetString()).ToList() ?? new List<string>();
        }

        public override string ToString()
        {
            return "Google";
        }
    }
}