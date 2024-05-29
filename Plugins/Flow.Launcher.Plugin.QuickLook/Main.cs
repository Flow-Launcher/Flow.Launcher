using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.QuickLook.Helpers;

namespace Flow.Launcher.Plugin.QuickLook
{
    public class Main : IAsyncPlugin, IAsyncExternalPreview, IPluginI18n
    {
        internal static PluginInitContext Context { get; set; }

        public Task InitAsync(PluginInitContext context)
        {
            Context = context;

            // prompt quicklook install if not found?

            return Task.CompletedTask;
        }

        public async Task TogglePreviewAsync(string path)
        {
            await QuickLookHelper.ToggleQuickLookAsync(path).ConfigureAwait(false);
        }
        public async Task ClosePreviewAsync()
        {
            await QuickLookHelper.CloseQuickLookAsync().ConfigureAwait(false);
        }

        public async Task SwitchPreviewAsync(string path, bool sendFailToast = true)
        {
            await QuickLookHelper.SwitchQuickLookAsync(path, sendFailToast).ConfigureAwait(false);
        }

        public async Task OpenPreviewAsync(string path, bool sendFailToast = true)
        {
            await QuickLookHelper.OpenQuickLookAsync(path, sendFailToast).ConfigureAwait(false);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token) => new List<Result>();

        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("plugin_description");
        }
    }
}
