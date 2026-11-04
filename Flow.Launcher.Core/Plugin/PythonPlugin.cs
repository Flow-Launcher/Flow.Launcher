using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    internal class PythonPlugin : JsonRPCPlugin
    {
        private readonly ProcessStartInfo _startInfo;
        private readonly string _pythonExecutable;
        private readonly PythonDependencyInstaller _installer;

        public PythonPlugin(string filename, PythonDependencyInstaller installer)
        {
            _pythonExecutable = filename;
            _installer = installer;
            _startInfo = new ProcessStartInfo
            {
                FileName = filename,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var path = Path.Combine(Constant.ProgramDirectory, JsonRPC);
            _startInfo.EnvironmentVariables["PYTHONPATH"] = path;
            // Prevent Python from writing .py[co] files.
            // Because .pyc contains location infos which will prevent python portable.
            _startInfo.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";

            _startInfo.EnvironmentVariables["FLOW_VERSION"] = Constant.Version;
            _startInfo.EnvironmentVariables["FLOW_PROGRAM_DIRECTORY"] = Constant.ProgramDirectory;
            _startInfo.EnvironmentVariables["FLOW_APPLICATION_DIRECTORY"] = Constant.ApplicationDirectory;
        }

        protected override Task<Stream> RequestAsync(JsonRPCRequestModel request, CancellationToken token = default)
        {
            _startInfo.ArgumentList[2] = JsonSerializer.Serialize(request, RequestSerializeOption);

            return ExecuteAsync(_startInfo, token);
        }

        protected override string Request(JsonRPCRequestModel rpcRequest, CancellationToken token = default)
        {
            // since this is not static, request strings will build up in ArgumentList if index is not specified
            _startInfo.ArgumentList[2] = JsonSerializer.Serialize(rpcRequest, RequestSerializeOption);
            _startInfo.WorkingDirectory = Context.CurrentPluginMetadata.PluginDirectory;
            // TODO: Async Action
            return Execute(_startInfo);
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

            foreach (var argument in PythonLaunchArguments.Build(metadata, sitePackages, v1: true))
            {
                _startInfo.ArgumentList.Add(argument);
            }

            // Plugins always expect the JSON data to be in the third argument
            // (we're always setting it as _startInfo.ArgumentList[2] = ...).
            _startInfo.ArgumentList.Add("");

            await base.InitAsync(context);
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;
        }
    }
}
