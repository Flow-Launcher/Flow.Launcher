using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.Plugin
{
    internal sealed class ExecutablePluginV2 : ProcessStreamPluginV2
    {
        protected override ProcessStartInfo StartInfo { get; set; }

        public ExecutablePluginV2(string filename)
        {
            StartInfo = new ProcessStartInfo { FileName = filename };
        }

        protected override MessageHandlerType MessageHandler { get; } = MessageHandlerType.NewLineDelimited;
    }
}
