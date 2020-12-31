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

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    class Bing : SuggestionSource
    {
        public override async Task<List<string>> Suggestions(string query)
        {
            Stream resultStream;

            try
            {
                const string api = "https://api.bing.com/qsonhs.aspx?q=";
                resultStream = await Http.GetStreamAsync(api + Uri.EscapeUriString(query)).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                Log.Exception("|Bing.Suggestions|Can't get suggestion from Bing", e);
                return new List<string>();
            }

            if (resultStream.Length == 0) return new List<string>();

            JsonElement json;
            try
            {
                json = (await JsonDocument.ParseAsync(resultStream)).RootElement.GetProperty("AS");
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
