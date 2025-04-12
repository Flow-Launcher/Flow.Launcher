using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    public abstract class SuggestionSource
    {
        public abstract Task<List<string>> SuggestionsAsync(string query, CancellationToken token);
    }
}
