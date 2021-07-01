using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    internal class PythonPlugin : JsonRPCPlugin
    {
        private readonly ProcessStartInfo _startInfo;
        public override string SupportedLanguage { get; set; } = AllowedLanguage.Python;

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

            // temp fix for issue #667
            var path = Path.Combine(Constant.ProgramDirectory, JsonRPC);
            _startInfo.EnvironmentVariables["PYTHONPATH"] = path;

            //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
            _startInfo.ArgumentList.Add("-B");
        }

        protected override Task<Stream> ExecuteQueryAsync(Query query, CancellationToken token)
        {
            JsonRPCServerRequestModel request = new JsonRPCServerRequestModel
            {
                Method = "query", Parameters = new object[] {query.Search},
            };

            _startInfo.ArgumentList[2] = request.ToString();

            // todo happlebao why context can't be used in constructor
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;

            return ExecuteAsync(_startInfo, token);
        }

        protected override string ExecuteCallback(JsonRPCRequestModel rpcRequest)
        {
            _startInfo.ArgumentList[2] = rpcRequest.ToString();
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;
            // TODO: Async Action
            return Execute(_startInfo);
        }

        protected override string ExecuteContextMenu(Result selectedResult)
        {
            JsonRPCServerRequestModel request = new JsonRPCServerRequestModel
            {
                Method = "context_menu", Parameters = new object[] {selectedResult.ContextData},
            };
            _startInfo.ArgumentList[2] = request.ToString();
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;

            // TODO: Async Action
            return Execute(_startInfo);
        }

        public override Task InitAsync(PluginInitContext context)
        {
            this.context = context;
            _startInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
            _startInfo.ArgumentList.Add("");
            return Task.CompletedTask;
        }
    }
}