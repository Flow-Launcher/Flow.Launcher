using System.Collections.Generic;

namespace Flow.Launcher.Plugin.PluginsManager.ViewModels
{
    internal class SettingsViewModel
    {
        internal Settings Settings { get; set; }

        internal PluginInitContext Context { get; set; }

        public SettingsViewModel(PluginInitContext context, Settings settings)
        {
            Context = context;
            Settings = settings;
            PluginModifiedActions = new List<PluginModifiedActionOption>
            {
                new(PluginModifiedAction.HotReload,
                    context.API.GetTranslation("plugin_pluginsmanager_plugin_settings_modified_action_hot_reload")),
                new(PluginModifiedAction.AutoRestart,
                    context.API.GetTranslation("plugin_pluginsmanager_plugin_settings_modified_action_auto_restart")),
                new(PluginModifiedAction.Manual,
                    context.API.GetTranslation("plugin_pluginsmanager_plugin_settings_modified_action_manual")),
            };
        }

        public bool WarnFromUnknownSource
        {
            get => Settings.WarnFromUnknownSource;
            set => Settings.WarnFromUnknownSource = value;
        }

        public PluginModifiedAction PluginModifiedAction
        {
            get => Settings.PluginModifiedAction;
            set => Settings.PluginModifiedAction = value;
        }

        public List<PluginModifiedActionOption> PluginModifiedActions { get; }

        public record PluginModifiedActionOption(PluginModifiedAction Value, string Display);
    }
}
