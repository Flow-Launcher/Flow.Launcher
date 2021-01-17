using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System.Net.Http;
using System.Threading;
using System.Text.Json;
using System.IO;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    public class Google : SuggestionSource
    {
        public override async Task<List<string>> Suggestions(string query, CancellationToken token)
        {
            JsonDocument json;

            try
            {
                const string api = "https://www.google.com/complete/search?output=chrome&q=";

                using var resultStream = await Http.GetStreamAsync(api + Uri.EscapeUriString(query)).ConfigureAwait(false);
                
                if (resultStream.Length == 0) 
                    return new List<string>();
                
                json = await JsonDocument.ParseAsync(resultStream);

            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (HttpRequestException e)
            {
                Log.Exception("|Google.Suggestions|Can't get suggestion from google", e);
                return new List<string>();
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