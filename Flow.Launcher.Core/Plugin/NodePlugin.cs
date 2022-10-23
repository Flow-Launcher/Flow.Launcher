using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Execution of JavaScript & TypeScript plugins
    /// </summary>
    internal class NodePlugin : JsonRPCPlugin
    {
        public override string SupportedLanguage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public NodePlugin(string filename)
        {

        }

        protected override string Request(JsonRPCRequestModel rpcRequest, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        protected override Task<Stream> RequestAsync(JsonRPCRequestModel rpcRequest, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
    }
}
