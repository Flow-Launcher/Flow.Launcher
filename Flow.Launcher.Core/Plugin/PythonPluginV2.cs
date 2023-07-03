using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Flow.Launcher.Core.Plugin.JsonRPCV2Models;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

namespace Flow.Launcher.Core.Plugin
{
    internal class PythonPluginV2 : JsonRPCPluginV2, IReloadable, IDisposable
    {
        private readonly ProcessStartInfo _startInfo;
        private Process _process;

        public override string SupportedLanguage { get; set; } = AllowedLanguage.Python;

        protected override JsonRpc RPC { get; set; }


        public PythonPluginV2(string filename)
        {
            _startInfo = new ProcessStartInfo
            {
                FileName = filename,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            // temp fix for issue #667
            var path = Path.Combine(Constant.ProgramDirectory, JsonRpc);
            _startInfo.EnvironmentVariables["PYTHONPATH"] = path;

            _startInfo.EnvironmentVariables["FLOW_VERSION"] = Constant.Version;
            _startInfo.EnvironmentVariables["FLOW_PROGRAM_DIRECTORY"] = Constant.ProgramDirectory;
            _startInfo.EnvironmentVariables["FLOW_APPLICATION_DIRECTORY"] = Constant.ApplicationDirectory;


            //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
            _startInfo.ArgumentList.Add("-B");
        }


        public override List<Result> LoadContextMenus(Result selectedResult)
        {
            throw new NotImplementedException();
        }

        public override async Task InitAsync(PluginInitContext context)
        {
            _startInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;

            _process = Process.Start(_startInfo);

            ArgumentNullException.ThrowIfNull(_process);

            SetupJsonRPC(_process, context.API);

            await base.InitAsync(context);
        }

        public void Dispose()
        {
            _process.Kill(true);
            _process.Dispose();
            base.Dispose();
        }

        public void ReloadData()
        {
            var oldProcess = _process;
            _process = Process.Start(_startInfo);
            ArgumentNullException.ThrowIfNull(_process);
            SetupJsonRPC(_process, Context.API);
            oldProcess.Kill(true);
            oldProcess.Dispose();
        }

        private void SetupJsonRPC(Process process, IPublicAPI api)
        {
            var formatter = new JsonMessageFormatter();
            var handler = new NewLineDelimitedMessageHandler(process.StandardInput.BaseStream,
                process.StandardOutput.BaseStream,
                formatter);

            ErrorStream = _process.StandardError;

            RPC = new JsonRpc(handler, new JsonRPCPublicAPI(api));
            RPC.SynchronizationContext = null;
            RPC.StartListening();
        }
    }
}
