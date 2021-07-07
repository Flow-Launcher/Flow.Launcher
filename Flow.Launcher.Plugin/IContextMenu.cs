using System.Collections.Generic;

namespace Flow.Launcher.Plugin
{
    public interface IContextMenu : IFeatures
    {
        List<Result> LoadContextMenus(Result selectedResult);
    }
}