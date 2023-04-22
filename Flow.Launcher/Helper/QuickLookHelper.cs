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
    internal class QuickLookHelper
    {
        private const int TIMEOUT = 500;
        private static readonly string pipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";
        private static readonly string pipeMessageSwitch = "QuickLook.App.PipeMessages.Switch";
        private static readonly string pipeMessageToggle = "QuickLook.App.PipeMessages.Toggle";

        /// <summary>
        /// Toggle QuickLook
        /// </summary>
        /// <param name="path">File path to preview</param>
        /// <param name="switchPreview">Is swtiching file</param>
        /// <returns></returns>
        public static async Task ToggleQuickLookPreviewAsync(string path, bool switchPreview = false)
        {
            bool isQuickLookAvailable = await DetectQuickLookAvailabilityAsync();

            if (!isQuickLookAvailable)
            {
                if (!switchPreview)
                {
                    Log.Warn($"{nameof(QuickLookHelper)}", "QuickLook not detected");
                }
                return;
            }

            string pipeName = $"QuickLook.App.Pipe.{WindowsIdentity.GetCurrent().User?.Value}";
            string message = switchPreview ? pipeMessageSwitch : pipeMessageToggle;
            
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            try
            {
                await client.ConnectAsync(TIMEOUT);

                await using var writer = new StreamWriter(client);
                await writer.WriteLineAsync($"{message}|{path}");
                await writer.FlushAsync();
            }
            catch (TimeoutException)
            {
                client.Close();
            }
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
                catch (TimeoutException)
                {
                    client.Close();
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
