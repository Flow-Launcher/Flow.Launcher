using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal class Settings : IJsonOnDeserialized
    {
        internal const string InstallCommand = "install";

        internal const string UninstallCommand = "uninstall";

        internal const string UpdateCommand = "update";

        public bool WarnFromUnknownSource { get; set; } = true;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PluginModifiedAction PluginModifiedAction { get; set; } = PluginModifiedAction.HotReload;

        // Legacy flags replaced by PluginModifiedAction; kept only so OnDeserialized can migrate old config files
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? AutoRestartAfterChanging { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? HotReloadAfterChanging { get; set; }

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (HotReloadAfterChanging == false)
            {
                PluginModifiedAction = AutoRestartAfterChanging == true
                    ? PluginModifiedAction.AutoRestart
                    : PluginModifiedAction.Manual;
            }
            AutoRestartAfterChanging = null;
            HotReloadAfterChanging = null;
        }
    }
}
