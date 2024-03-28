using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Flow.Launcher.Core.Plugin.JsonRPCV2Models;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Streams;
using StreamJsonRpc;

namespace Flow.Launcher.Core.Plugin
{
    internal sealed class PythonPluginV2 : ProcessStreamPluginV2
    {
        protected override ProcessStartInfo StartInfo { get; set; }

        public PythonPluginV2(string filename)
        {
            StartInfo = new ProcessStartInfo { FileName = filename, };

            var path = Path.Combine(Constant.ProgramDirectory, JsonRpc);
            StartInfo.EnvironmentVariables["PYTHONPATH"] = path;

            //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
            StartInfo.ArgumentList.Add("-B");
        }

        public override async Task InitAsync(PluginInitContext context)
        {
            StartInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
            await base.InitAsync(context);
        }

        protected override MessageHandlerType MessageHandler { get; } = MessageHandlerType.NewLineDelimited;
    }
}
