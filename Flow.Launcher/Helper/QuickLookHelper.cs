// Adapted from Files
// https://github.com/files-community/Files/blob/ad33c75c53382fcb9b16fa9cd66ae5399f3dff0b/src/Files.App/Helpers/QuickLookHelpers.cs
using System;
using System.IO.Pipes;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Helper
{
    internal static class QuickLookHelper
    {
        private const int TIMEOUT = 500;
        private static DateTime lastNotificationTime = DateTime.MinValue;
        
        private static readonly string pipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";
        private static readonly string pipeMessageSwitch = "QuickLook.App.PipeMessages.Switch";
        private static readonly string pipeMessageToggle = "QuickLook.App.PipeMessages.Toggle";
        private static readonly string pipeMessageClose = "QuickLook.App.PipeMessages.Close";
        private static readonly string pipeMessageInvoke = "QuickLook.App.PipeMessages.Invoke";


        /// <summary>
        /// Toggle QuickLook
        /// </summary>
        /// <param name="path">File path to preview</param>
        /// <param name="sendFailToast">Send toast when fails.</param>
        /// <returns></returns>
        public static async Task<bool> ToggleQuickLookAsync(string path, bool sendFailToast = true)
        {           
            if (string.IsNullOrEmpty(path))
                return false;

            bool success = await SendQuickLookPipeMsgAsync(pipeMessageToggle, path);
            if (sendFailToast && !success)
            {
                ShowQuickLookUnavailableToast();
            }
            return success;
        }

        public static async Task<bool> CloseQuickLookAsync()
        {
            bool success = await SendQuickLookPipeMsgAsync(pipeMessageClose);
            return success;
        }

        public static async Task<bool> OpenQuickLookAsync(string path, bool sendFailToast = true)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            bool success = await SendQuickLookPipeMsgAsync(pipeMessageInvoke, path);
            if (sendFailToast && !success)
            {
                ShowQuickLookUnavailableToast();
            }
            return success;
        }

        /// <summary>
        /// Switch QuickLook to preview another file if it's on
        /// </summary>
        /// <param name="path">File path to preview</param>
        /// <param name="sendFailToast">Send notification if fail</param>
        /// <returns></returns>
        public static async Task<bool> SwitchQuickLookAsync(string path, bool sendFailToast = true)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            bool success = await SendQuickLookPipeMsgAsync(pipeMessageSwitch, path);
            if (sendFailToast && !success)
            {
                ShowQuickLookUnavailableToast();
            }
            return success;
        }

        private static async Task<bool> SendQuickLookPipeMsgAsync(string message, string arg = "")
        {
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            try
            {
                await client.ConnectAsync(TIMEOUT);

                await using var writer = new StreamWriter(client);
                await writer.WriteLineAsync($"{message}|{arg}");
                await writer.FlushAsync();
            }
            catch (TimeoutException)
            {
                client.Close();
                Log.Error($"{nameof(QuickLookHelper)}", "QuickLook timeout");
                return false;
            }
            catch (Exception e)
            {
                Log.Exception($"{nameof(QuickLookHelper)}", "QuickLook error", e);
                return false;
            }
            return true;
        }

        public static async Task<bool> DetectQuickLookAvailabilityAsync()
        {
            static async Task<int> QuickLookServerAvailable()
            {
                await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                try
                {
                    await client.ConnectAsync(TIMEOUT);
                    var serverInstances = client.NumberOfServerInstances;

                    await using var writer = new StreamWriter(client);
                    await writer.WriteLineAsync($"{pipeMessageSwitch}|");
                    await writer.FlushAsync();

                    return serverInstances;
                }
                catch (TimeoutException e)
                {
                    client.Close();
                    Log.Exception($"{nameof(QuickLookHelper)}", "QuickLook connection timeout", e);
                    return 0;
                }
            }

            try
            {
                var result = await QuickLookServerAvailable();
                return result != 0;
            }
            catch (Exception e)
            {
                Log.Exception($"{nameof(QuickLookHelper)}", "QuickLook unavailable", e);
                return false;
            }
        }
        
        private static void ShowQuickLookUnavailableToast()
        {
            if (lastNotificationTime.AddSeconds(10) < DateTime.Now)
            {
                Notification.Show(InternationalizationManager.Instance.GetTranslation("QuickLookFail"),
                              InternationalizationManager.Instance.GetTranslation("QuickLookFailTips"));
                lastNotificationTime = DateTime.Now;
            }
        }
    }
}
