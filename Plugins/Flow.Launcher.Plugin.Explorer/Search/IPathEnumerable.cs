using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public interface IPathEnumerable
    {
        public IEnumerable<SearchResult> Enumerate(string path, bool recursive);
    }
}