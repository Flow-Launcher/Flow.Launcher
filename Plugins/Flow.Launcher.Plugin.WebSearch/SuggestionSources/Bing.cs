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
        public override async Task<List<string>> Suggestions(string query, CancellationToken token)
        {

            try
            {
                const string api = "https://api.bing.com/qsonhs.aspx?q=";

                using var resultStream = await Main.Context.API.HttpGetStreamAsync(api + Uri.EscapeUriString(query), token).ConfigureAwait(false);

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
            catch (Exception e) when (e is HttpRequestException || e.InnerException is TimeoutException)
            {
                Main.Context.API.LogException("Bing.Suggestion", "Can't get suggestion from baidu", e);
                return null;
            }
            catch (JsonException e)
            {
                Main.Context.API.LogException("Baidu.Suggestion", "Can't parse suggestions from bing", e);
                return new List<string>();
            }
        }

        public override string ToString()
        {
            return "Bing";
        }
    }
}
