using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    public interface IPlugin
    {
        List<Result> Query(Query query);
        Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            return Task.Run(() => Query(query), token);
        }
        void Init(PluginInitContext context);
    }
}