using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.WebSearch.SuggestionSources
{
    public abstract class SuggestionSource
    {
        public abstract Task<List<string>> Suggestions(string query, CancellationToken token);
    }
}
