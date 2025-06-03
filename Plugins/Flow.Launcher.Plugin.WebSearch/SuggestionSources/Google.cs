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
        private static readonly string ClassName = nameof(Google);

        public override async Task<List<string>> SuggestionsAsync(string query, CancellationToken token)
        {
            try
            {
                const string api = "https://www.google.com/complete/search?output=chrome&q=";

                await using var resultStream = await Main._context.API.HttpGetStreamAsync(api + Uri.EscapeDataString(query), token: token).ConfigureAwait(false);

                using var json = await JsonDocument.ParseAsync(resultStream, cancellationToken: token);

                var results = json.RootElement.EnumerateArray().ElementAt(1);

                return results.EnumerateArray().Select(o => o.GetString()).ToList();

            }
            catch (Exception e) when (e is HttpRequestException or {InnerException: TimeoutException})
            {
                Main._context.API.LogException(ClassName, "Can't get suggestion from Google", e);
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
            return "Google";
        }
    }
}
