using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    public interface IFeatures
    {
    }

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
        public CancellationToken Token { get; init; } = default;
    }

    /// <summary>
    /// This interface is to indicate and allow plugins to asyncronously reload their
    /// in memory data cache or other mediums when user makes a new change
    /// that is not immediately captured. For example, for BrowserBookmark and Program
    /// plugin does not automatically detect when a user added a new bookmark or program,
    /// so this interface's function is exposed to allow user manually do the reloading after 
    /// those new additions.
    /// 
    /// The command that allows user to manual reload is exposed via Plugin.Sys, and
    /// it will call the plugins that have implemented this interface.
    /// </summary>
    public interface IAsyncReloadable : IFeatures
    {
        Task ReloadDataAsync();
    }

    /// <summary>
    /// This interface is to indicate and allow plugins to synchronously reload their
    /// in memory data cache or other mediums when user makes a new change
    /// that is not immediately captured. For example, for BrowserBookmark and Program
    /// plugin does not automatically detect when a user added a new bookmark or program,
    /// so this interface's function is exposed to allow user manually do the reloading after 
    /// those new additions.
    /// 
    /// The command that allows user to manual reload is exposed via Plugin.Sys, and
    /// it will call the plugins that have implemented this interface.
    /// 
    /// <para>
    /// If requiring reloading data asynchronously, please use the IAsyncReloadable interface
    /// </para>
    /// </summary>
    public interface IReloadable : IFeatures
    {
        void ReloadData();
    }

    /// <summary>
    /// Save addtional plugin data. Inherit this interface if additional data e.g. cache needs to be saved,
    /// Otherwise if LoadSettingJsonStorage or SaveSettingJsonStorage has been callded,
    /// plugin settings will be automatically saved (see Flow.Launcher/PublicAPIInstance.SavePluginSettings) by Flow
    /// </summary>
    public interface ISavable : IFeatures
    {
        void Save();
    }
}