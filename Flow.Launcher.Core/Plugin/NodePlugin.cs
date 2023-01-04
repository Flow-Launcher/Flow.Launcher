using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Execution of JavaScript & TypeScript plugins
    /// </summary>
    internal class NodePlugin : JsonRPCPlugin
    {
        private readonly ProcessStartInfo _startInfo;

        public NodePlugin(string filename)
        {
            _startInfo = new ProcessStartInfo
            {
                FileName = filename,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        protected override Task<Stream> RequestAsync(JsonRPCRequestModel request, CancellationToken token = default)
        {
            _startInfo.ArgumentList[1] = request.ToString();
            return ExecuteAsync(_startInfo, token);
        }

        protected override string Request(JsonRPCRequestModel rpcRequest, CancellationToken token = default)
        {
            // since this is not static, request strings will build up in ArgumentList if index is not specified
            _startInfo.ArgumentList[1] = rpcRequest.ToString();
            return Execute(_startInfo);
        }

        public override async Task InitAsync(PluginInitContext context)
        {
            _startInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
            _startInfo.ArgumentList.Add(string.Empty);
            await base.InitAsync(context);
            _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;
        }
    }
}
