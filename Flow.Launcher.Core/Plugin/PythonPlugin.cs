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

            _startInfo.EnvironmentVariables["FLOW_VERSION"] = Constant.Version;
            _startInfo.EnvironmentVariables["FLOW_PROGRAM_DIRECTORY"] = Constant.ProgramDirectory;
            _startInfo.EnvironmentVariables["FLOW_APPLICATION_DIRECTORY"] = Constant.ApplicationDirectory;


            //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
            _startInfo.ArgumentList.Add("-B");
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
            _startInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
            _startInfo.ArgumentList.Add("");
            await base.InitAsync(context);
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;
        }
    }
}
