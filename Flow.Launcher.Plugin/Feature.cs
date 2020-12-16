using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Flow.Launcher.Plugin
{
    public interface IFeatures { }

    public interface IContextMenu : IFeatures
    {
        List<Result> LoadContextMenus(Result selectedResult);
    }

    /// <summary>
    /// Represent plugins that support internationalization
    /// </summary>
    public interface IPluginI18n : IFeatures
    {
        string GetTranslatedPluginTitle();

        string GetTranslatedPluginDescription();
    }

    public interface IResultUpdated : IFeatures
    {
        event ResultUpdatedEventHandler ResultsUpdated;
    }

    public delegate void ResultUpdatedEventHandler(IResultUpdated sender, ResultUpdatedEventArgs e);

    public class ResultUpdatedEventArgs : EventArgs
    {
        public List<Result> Results;
        public Query Query;
    }
}
