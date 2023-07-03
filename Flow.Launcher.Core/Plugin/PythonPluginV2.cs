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
    internal class PythonPluginV2 : JsonRPCPluginV2
    {
        private readonly ProcessStartInfo _startInfo;
        private Process _process;

        public override string SupportedLanguage { get; set; } = AllowedLanguage.Python;

        protected override IDuplexPipe ClientPipe { get; set; }


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
            
            SetupPipe(_process);

            await base.InitAsync(context);
        }

        public override async ValueTask DisposeAsync()
        {
            _process.Kill(true);
            await _process.WaitForExitAsync();
            _process.Dispose();
            await base.DisposeAsync();
        }

        private void SetupPipe(Process process)
        {
            var (reader, writer) = (PipeReader.Create(process.StandardOutput.BaseStream),
                PipeWriter.Create(process.StandardInput.BaseStream));
            ClientPipe = new DuplexPipe(reader, writer);
        }
        
        public override async Task ReloadDataAsync()
        {
            var oldProcess = _process;
            _process = Process.Start(_startInfo);
            ArgumentNullException.ThrowIfNull(_process);
            SetupPipe(_process);
            await base.ReloadDataAsync();
            oldProcess.Kill(true);
            await oldProcess.WaitForExitAsync();
            oldProcess.Dispose();
        }
    }
}
