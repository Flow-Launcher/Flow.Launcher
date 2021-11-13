using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Text.Json;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    public class Google : SuggestionSource
    {
        public override async Task<List<string>> Suggestions(string query, CancellationToken token)
        {
            try
            {
                const string api = "https://www.google.com/complete/search?output=chrome&q=";

                using var resultStream = await Main.Context.API.HttpGetStreamAsync(api + Uri.EscapeUriString(query), token).ConfigureAwait(false);

                using var json = await JsonDocument.ParseAsync(resultStream, cancellationToken: token);

                if (json == null)
                    return new List<string>();

                var results = json.RootElement.EnumerateArray().ElementAt(1);

                return results.EnumerateArray().Select(o => o.GetString()).ToList();

            }
            catch (Exception e) when (e is HttpRequestException || e.InnerException is TimeoutException)
            {
                Main.Context.API.LogException("Google.Suggestions", "Can't get suggestion from Google", e);
                return null;
            }
            catch (JsonException e)
            {
                Main.Context.API.LogException("Google.Suggestions", "Can't parse suggestion from Google", e);
                return new List<string>();
            }
        }

        public override string ToString()
        {
            return "Google";
        }
    }
}