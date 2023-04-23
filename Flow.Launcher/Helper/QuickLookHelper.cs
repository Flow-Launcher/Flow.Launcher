// Adapted from Files
// https://github.com/files-community/Files/blob/ad33c75c53382fcb9b16fa9cd66ae5399f3dff0b/src/Files.App/Helpers/QuickLookHelpers.cs
using System;
using System.IO.Pipes;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Helper
{
    internal static class QuickLookHelper
    {
        private const int TIMEOUT = 500;
        private static readonly string pipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";
        private static readonly string pipeMessageSwitch = "QuickLook.App.PipeMessages.Switch";
        private static readonly string pipeMessageToggle = "QuickLook.App.PipeMessages.Toggle";
        private static readonly string pipeMessageClose = "QuickLook.App.PipeMessages.Close";
        private static readonly string pipeMessageInvoke = "QuickLook.App.PipeMessages.Invoke";

        /// <summary>
        /// Toggle QuickLook
        /// </summary>
        /// <param name="path">File path to preview</param>
        /// <param name="switchPreview">Is swtiching file</param>
        /// <returns></returns>
        public static async Task<bool> ToggleQuickLookAsync(string path, bool switchPreview = false)
        {
            //bool isQuickLookAvailable = await DetectQuickLookAvailabilityAsync();

            //if (!isQuickLookAvailable)
            //{
            //    if (!switchPreview)
            //    {
            //        Log.Warn($"{nameof(QuickLookHelper)}", "QuickLook not detected");
            //    }
            //    return;
            //}

            if (string.IsNullOrEmpty(path))
                return false;
            
            return await SendQuickLookPipeMsgAsync(switchPreview ? pipeMessageSwitch : pipeMessageToggle, path);
        }

        public static async Task<bool> CloseQuickLookAsync()
        {
            return await SendQuickLookPipeMsgAsync(pipeMessageClose);
        }

        public static async Task<bool> OpenQuickLookAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            return await SendQuickLookPipeMsgAsync(pipeMessageInvoke, path);
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

        private static async Task<bool> DetectQuickLookAvailabilityAsync()
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
    }
}
