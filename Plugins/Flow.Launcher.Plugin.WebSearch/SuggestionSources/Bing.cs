using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Threading;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    public class Bing : SuggestionSource
    {
        private static readonly string ClassName = nameof(Bing);

        public override async Task<List<string>> SuggestionsAsync(string query, CancellationToken token)
        {
            try
            {
                const string api = "https://api.bing.com/qsonhs.aspx?q=";

                await using var resultStream = await Main._context.API.HttpGetStreamAsync(api + Uri.EscapeDataString(query), token).ConfigureAwait(false);

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
                Main._context.API.LogException(ClassName, "Can't get suggestion from Bing", e);
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
            return "Bing";
        }
    }
}
