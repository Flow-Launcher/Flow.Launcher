using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.Explorer.Exceptions;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    internal class EverythingAvailabilityService
    {
        private readonly Settings _settings;

        public EverythingAvailabilityService(Settings settings)
        {
            _settings = settings;
        }

        public async ValueTask EnsureAvailableAsync(IEverythingApi api, CancellationToken token = default)
        {
            try
            {
                if (!await api.IsEverythingRunningAsync(token))
                    throw new EngineNotAvailableException(
                        Enum.GetName(Settings.IndexSearchEngineOption.Everything)!,
                        Localize.flowlauncher_plugin_everything_click_to_launch_or_install(),
                        Localize.flowlauncher_plugin_everything_is_not_running(),
                        Constants.EverythingErrorImagePath,
                        ClickToInstallEverythingAsync);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                throw new EngineNotAvailableException(
                    Enum.GetName(Settings.IndexSearchEngineOption.Everything)!,
                    Localize.flowlauncher_plugin_everything_architecture_check(),
                    Constants.GeneralSearchErrorImagePath,
                    Localize.flowlauncher_plugin_everything_sdk_issue());
            }
        }

        private async ValueTask<bool> ClickToInstallEverythingAsync(ActionContext _)
        {
            try
            {
                var installedPath = await EverythingDownloadHelper.PromptDownloadIfNotInstallAsync(_settings.EverythingInstalledPath, Main.Context.API);

                if (installedPath == null)
                {
                    Main.Context.API.ShowMsgError(Localize.flowlauncher_plugin_everything_not_found());
                    Main.Context.API.LogError(nameof(EverythingAvailabilityService), "Unable to find Everything.exe");

                    return false;
                }

                _settings.EverythingInstalledPath = installedPath;
                Process.Start(installedPath, "-startup");

                return true;
            }
            catch (Exception e)
            {
                Main.Context.API.ShowMsgError(Localize.flowlauncher_plugin_everything_install_issue());
                Main.Context.API.LogException(nameof(EverythingAvailabilityService), "Failed to install Everything", e);

                return false;
            }
        }
    }
}
