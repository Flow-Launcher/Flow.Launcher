using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
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

        private readonly string _pythonExecutable;
        private readonly PythonDependencyInstaller _installer;

        public PythonPluginV2(string filename, PythonDependencyInstaller installer)
        {
            _pythonExecutable = filename;
            _installer = installer;
            StartInfo = new ProcessStartInfo { FileName = filename, };

            var path = Path.Combine(Constant.ProgramDirectory, JsonRpc);
            StartInfo.EnvironmentVariables["PYTHONPATH"] = path;
            StartInfo.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
        }

        public override async Task InitAsync(PluginInitContext context)
        {
            var metadata = context.CurrentPluginMetadata;
            string sitePackages = null;
            if (metadata.Dependencies is { Count: > 0 })
            {
                _installer.CleanupStaleInstalls(metadata.ID);
                sitePackages = await _installer.EnsureInstalledAsync(metadata, _pythonExecutable, CancellationToken.None);
            }

            foreach (var argument in PythonLaunchArguments.Build(metadata, sitePackages, v1: false))
            {
                StartInfo.ArgumentList.Add(argument);
            }

            await base.InitAsync(context);
        }

        protected override MessageHandlerType MessageHandler { get; } = MessageHandlerType.NewLineDelimited;
    }
}
