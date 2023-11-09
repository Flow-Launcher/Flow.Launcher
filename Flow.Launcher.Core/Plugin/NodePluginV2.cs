using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Execution of JavaScript & TypeScript plugins
    /// </summary>
    internal class NodePluginV2 : ProcessStreamPluginV2
    {
        public NodePluginV2(string filename)
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = filename,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        public override string SupportedLanguage { get; set; }
        protected override ProcessStartInfo StartInfo { get; set; }

        public override async Task InitAsync(PluginInitContext context)
        {
            StartInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
            StartInfo.ArgumentList.Add(string.Empty);
            StartInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;
            await base.InitAsync(context);
        }
    }
}
