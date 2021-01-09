using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    public interface IAsyncPlugin
    {
        Task<List<Result>> QueryAsync(Query query, CancellationToken token);
        Task InitAsync(PluginInitContext context);
    }
}