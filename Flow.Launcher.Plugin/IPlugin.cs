using System.Collections.Generic;

namespace Flow.Launcher.Plugin
{
    public interface IPlugin
    {
        List<Result> Query(Query query);
        void Init(PluginInitContext context);
    }
}