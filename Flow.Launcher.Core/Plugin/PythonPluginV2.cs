using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

namespace Flow.Launcher.Core.Plugin
{
    internal class PythonPluginV2 : JsonRpcPluginV2
    {
        private readonly ProcessStartInfo _startInfo;
        private Process _process;

        public override string SupportedLanguage { get; set; } = AllowedLanguage.Python;

        protected override JsonRpc Rpc { get; set; }


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

            var formatter = new JsonMessageFormatter();
            var handler = new NewLineDelimitedMessageHandler(_process.StandardInput.BaseStream,
                _process.StandardOutput.BaseStream,
                formatter);

            Rpc = new JsonRpc(handler, context.API);
            Rpc.StartListening();

            _ = _process.StandardError.ReadToEndAsync().ContinueWith(e => throw new Exception(e.Result));

            await base.InitAsync(context);
        }
    }
}
