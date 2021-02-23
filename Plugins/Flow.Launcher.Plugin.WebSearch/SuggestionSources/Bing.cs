using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
            JsonElement json;

            try
            {
                const string api = "https://api.bing.com/qsonhs.aspx?q=";
                
                using var resultStream = await Http.GetStreamAsync(api + Uri.EscapeUriString(query), token).ConfigureAwait(false);

                json = (await JsonDocument.ParseAsync(resultStream, cancellationToken: token)).RootElement.GetProperty("AS");

            }
            catch (OperationCanceledException)
            {
                return new List<string>();
            }
            catch (HttpRequestException e)
            {
                Log.Exception("|Bing.Suggestions|Can't get suggestion from Bing", e);
                return new List<string>();
            }
            catch (JsonException e)
            {
                Log.Exception("|Bing.Suggestions|can't parse suggestions", e);
                return new List<string>();
            }

            if (json.GetProperty("FullResults").GetInt32() == 0)
                return new List<string>();

            return json.GetProperty("Results")
                       .EnumerateArray()
                       .SelectMany(r => r.GetProperty("Suggests")
                                         .EnumerateArray()
                                         .Select(s => s.GetProperty("Txt").GetString()))
                       .ToList();

        }

        public override string ToString()
        {
            return "Bing";
        }
    }
}
