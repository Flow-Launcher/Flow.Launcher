using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    internal class PythonPlugin : JsonRPCPlugin
    {
        private readonly ProcessStartInfo _startInfo;

        public PythonPlugin(string filename)
        {
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
            // Run .py files via `-c <code>`
            if (context.CurrentPluginMetadata.ExecuteFilePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                var rootDirectory = context.CurrentPluginMetadata.PluginDirectory;
                var libDirectory = Path.Combine(rootDirectory, "lib");
                var libPyWin32Directory = Path.Combine(libDirectory, "win32");
                var libPyWin32LibDirectory = Path.Combine(libPyWin32Directory, "lib");
                var pluginDirectory = Path.Combine(rootDirectory, "plugin");

                // This makes it easier for plugin authors to import their own modules.
                // They won't have to add `.`, `./lib`, or `./plugin` to their sys.path manually.
                // Instead of running the .py file directly, we pass the code we want to run as a CLI argument.
                // This code sets sys.path for the plugin author and then runs the .py file via runpy.
                _startInfo.ArgumentList.Add("-c");
                _startInfo.ArgumentList.Add(
                    $"""
                     import sys
                     sys.path.append(r'{rootDirectory}')
                     sys.path.append(r'{libDirectory}')
                     sys.path.append(r'{libPyWin32LibDirectory}')
                     sys.path.append(r'{libPyWin32Directory}')
                     sys.path.append(r'{pluginDirectory}')

                     import runpy
                     runpy.run_path(r'{context.CurrentPluginMetadata.ExecuteFilePath}', None, '__main__')
                     """
                );
                // Plugins always expect the JSON data to be in the third argument
                // (we're always setting it as _startInfo.ArgumentList[2] = ...).
                _startInfo.ArgumentList.Add("");
            }
            // Run .pyz files as is
            else
            {
                // No need for -B flag because we're using PYTHONDONTWRITEBYTECODE env variable now,
                // but the plugins still expect data to be sent as the third argument, so we're keeping
                // the flag here, even though it's not necessary anymore.
                _startInfo.ArgumentList.Add("-B");
                _startInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
                // Plugins always expect the JSON data to be in the third argument
                // (we're always setting it as _startInfo.ArgumentList[2] = ...).
                _startInfo.ArgumentList.Add("");
            }

            await base.InitAsync(context);
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;
        }
    }
}
