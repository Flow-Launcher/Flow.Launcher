using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using Nerdbank.Streams;

namespace Flow.Launcher.Core.Plugin
{
    internal abstract class ProcessStreamPluginV2 : JsonRPCPluginV2
    {

        public override string SupportedLanguage { get; set; }
        protected sealed override IDuplexPipe ClientPipe { get; set; }

        protected abstract ProcessStartInfo StartInfo { get; set; }

        protected Process ClientProcess { get; set; }

        public override async Task InitAsync(PluginInitContext context)
        {
            StartInfo.EnvironmentVariables["FLOW_VERSION"] = Constant.Version;
            StartInfo.EnvironmentVariables["FLOW_PROGRAM_DIRECTORY"] = Constant.ProgramDirectory;
            StartInfo.EnvironmentVariables["FLOW_APPLICATION_DIRECTORY"] = Constant.ApplicationDirectory;

            StartInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
            StartInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;

            ClientProcess = Process.Start(StartInfo);
            ArgumentNullException.ThrowIfNull(ClientProcess);

            SetupPipe(ClientProcess);

            ErrorStream = ClientProcess.StandardError;

            await base.InitAsync(context);
        }
        
        private void SetupPipe(Process process)
        {
            var (reader, writer) = (PipeReader.Create(process.StandardOutput.BaseStream),
                PipeWriter.Create(process.StandardInput.BaseStream));
            ClientPipe = new DuplexPipe(reader, writer);
        }
        
        
        public override async Task ReloadDataAsync()
        {
            var oldProcess = ClientProcess;
            ClientProcess = Process.Start(StartInfo);
            ArgumentNullException.ThrowIfNull(ClientProcess);
            SetupPipe(ClientProcess);
            await base.ReloadDataAsync();
            oldProcess.Kill(true);
            await oldProcess.WaitForExitAsync();
            oldProcess.Dispose();
        }
        
        
        public override async ValueTask DisposeAsync()
        {
            ClientProcess.Kill(true);
            await ClientProcess.WaitForExitAsync();
            ClientProcess.Dispose();
            await base.DisposeAsync();
        }
    }
}
