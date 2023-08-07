using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using System.Net.Http;
using System.Threading;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    public class Baidu : SuggestionSource
    {
        private readonly Regex _reg = new Regex("window.baidu.sug\\((.*)\\)");

        public override async Task<List<string>> SuggestionsAsync(string query, CancellationToken token)
        {
            string result;

            try
            {
                const string api = "http://suggestion.baidu.com/su?json=1&wd=";
                result = await Http.GetAsync(api + Uri.EscapeDataString(query), token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpRequestException or {InnerException: TimeoutException})
            {
                Log.Exception("|Baidu.Suggestions|Can't get suggestion from baidu", e);
                return null;
            }

            if (string.IsNullOrEmpty(result)) return new List<string>();
            Match match = _reg.Match(result);
            if (match.Success)
            {
                JsonDocument json;
                try
                {
                    json = JsonDocument.Parse(match.Groups[1].Value);
                }
                catch (JsonException e)
                {
                    Log.Exception("|Baidu.Suggestions|can't parse suggestions", e);
                    return new List<string>();
                }

                var results = json?.RootElement.GetProperty("s");

                return results?.EnumerateArray().Select(o => o.GetString()).ToList() ?? new List<string>();
            }

            return new List<string>();
        }

        public override string ToString()
        {
            return "Baidu";
        }
    }
}
