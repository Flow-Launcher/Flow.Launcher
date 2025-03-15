using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Threading;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    class Bing : SuggestionSource
    {
        public override async Task<List<string>> SuggestionsAsync(string query, CancellationToken token)
        {

            try
            {
                const string api = "https://api.bing.com/qsonhs.aspx?q=";

                await using var resultStream = await Http.GetStreamAsync(api + Uri.EscapeDataString(query), token).ConfigureAwait(false);

                using var json = (await JsonDocument.ParseAsync(resultStream, cancellationToken: token));
                var root = json.RootElement.GetProperty("AS");

                if (root.GetProperty("FullResults").GetInt32() == 0)
                    return new List<string>();

                return root.GetProperty("Results")
                    .EnumerateArray()
                    .SelectMany(r => r.GetProperty("Suggests")
                        .EnumerateArray()
                        .Select(s => s.GetProperty("Txt").GetString()))
                    .ToList();



            }
            catch (Exception e) when (e is HttpRequestException or { InnerException: TimeoutException })
            {
                Log.Exception("|Baidu.Suggestions|Can't get suggestion from baidu", e);
                return null;
            }
            catch (JsonException e)
            {
                Log.Exception("|Bing.Suggestions|can't parse suggestions", e);
                return new List<string>();
            }
        }

        public override string ToString()
        {
            return "Bing";
        }
    }
}
