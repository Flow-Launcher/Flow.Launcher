using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using Meziantou.Framework.Win32;
using Nerdbank.Streams;

#nullable enable

namespace Flow.Launcher.Core.Plugin
{
    internal abstract class ProcessStreamPluginV2 : JsonRPCPluginV2
    {
        private static readonly JobObject _jobObject = new();

        static ProcessStreamPluginV2()
        {
            _jobObject.SetLimits(new JobObjectLimits()
            {
                Flags = JobObjectLimitFlags.KillOnJobClose | JobObjectLimitFlags.DieOnUnhandledException |
                        JobObjectLimitFlags.SilentBreakawayOk
            });

            _jobObject.AssignProcess(Process.GetCurrentProcess());
        }

        protected sealed override IDuplexPipe ClientPipe { get; set; } = null!;

        protected abstract ProcessStartInfo StartInfo { get; set; }

        protected Process ClientProcess { get; set; } = null!;

        public override async Task InitAsync(PluginInitContext context)
        {
            StartInfo.EnvironmentVariables["FLOW_VERSION"] = Constant.Version;
            StartInfo.EnvironmentVariables["FLOW_PROGRAM_DIRECTORY"] = Constant.ProgramDirectory;
            StartInfo.EnvironmentVariables["FLOW_APPLICATION_DIRECTORY"] = Constant.ApplicationDirectory;

            StartInfo.RedirectStandardError = true;
            StartInfo.RedirectStandardInput = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;
            StartInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;

            var process = Process.Start(StartInfo);
            ArgumentNullException.ThrowIfNull(process);
            ClientProcess = process;
            _jobObject.AssignProcess(ClientProcess);

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
            ClientProcess = Process.Start(StartInfo)!;
            ArgumentNullException.ThrowIfNull(ClientProcess);
            SetupPipe(ClientProcess);
            await base.ReloadDataAsync();
            oldProcess.Kill(true);
            await oldProcess.WaitForExitAsync();
            oldProcess.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            ClientProcess.Kill(true);
            await ClientProcess.WaitForExitAsync();
            ClientProcess.Dispose();
        }
    }
}
