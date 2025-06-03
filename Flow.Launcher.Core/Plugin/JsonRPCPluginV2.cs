using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin.JsonRPCV2Models;
using Flow.Launcher.Plugin;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using IAsyncDisposable = System.IAsyncDisposable;

namespace Flow.Launcher.Core.Plugin
{
    internal abstract class JsonRPCPluginV2 : JsonRPCPluginBase, IAsyncDisposable, IAsyncReloadable, IResultUpdated
    {
        public const string JsonRpc = "JsonRPC";

        private static readonly string ClassName = nameof(JsonRPCPluginV2);

        protected abstract IDuplexPipe ClientPipe { get; set; }

        protected StreamReader ErrorStream { get; set; }

        private JsonRpc RPC { get; set; }

        protected override async Task<bool> ExecuteResultAsync(JsonRPCResult result)
        {
            var res = await RPC.InvokeAsync<JsonRPCExecuteResponse>(result.JsonRPCAction.Method,
                argument: result.JsonRPCAction.Parameters);

            return res.Hide;
        }

        private JoinableTaskFactory JTF { get; } = new JoinableTaskFactory(new JoinableTaskContext());

        public override List<Result> LoadContextMenus(Result selectedResult)
        {
            var res = JTF.Run(() => RPC.InvokeWithCancellationAsync<JsonRPCQueryResponseModel>("context_menu",
                new object[] { selectedResult.ContextData }));

            var results = ParseResults(res);

            return results;
        }

        public override async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var res = await RPC.InvokeWithCancellationAsync<JsonRPCQueryResponseModel>("query",
                new object[] { query, Settings.Inner },
                token);

            var results = ParseResults(res);

            return results;
        }

        public override async Task InitAsync(PluginInitContext context)
        {
            await base.InitAsync(context);

            SetupJsonRPC();

            _ = ReadErrorAsync();

            await RPC.InvokeAsync("initialize", context);

            async Task ReadErrorAsync()
            {
                var error = await ErrorStream.ReadToEndAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception(error);
                }
            }
        }

        public event ResultUpdatedEventHandler ResultsUpdated;

        protected enum MessageHandlerType
        {
            HeaderDelimited,
            LengthHeaderDelimited,
            NewLineDelimited
        }

        protected abstract MessageHandlerType MessageHandler { get; }

        private void SetupJsonRPC()
        {
            var formatter = new SystemTextJsonFormatter { JsonSerializerOptions = RequestSerializeOption };
            IJsonRpcMessageHandler handler = MessageHandler switch
            {
                MessageHandlerType.HeaderDelimited => new HeaderDelimitedMessageHandler(ClientPipe, formatter),
                MessageHandlerType.LengthHeaderDelimited => new LengthHeaderMessageHandler(ClientPipe, formatter),
                MessageHandlerType.NewLineDelimited => new NewLineDelimitedMessageHandler(ClientPipe, formatter),
                _ => throw new ArgumentOutOfRangeException()
            };

            RPC = new JsonRpc(handler, new JsonRPCPublicAPI(Context.API));

            RPC.AddLocalRpcMethod("UpdateResults", new Action<string, JsonRPCQueryResponseModel>((rawQuery, response) =>
            {
                var results = ParseResults(response);
                ResultsUpdated?.Invoke(this,
                    new ResultUpdatedEventArgs { Query = new Query() { RawQuery = rawQuery }, Results = results });
            }));
            RPC.SynchronizationContext = null;
            RPC.StartListening();
        }

        public virtual async Task ReloadDataAsync()
        {
            try
            {
                await RPC.InvokeAsync("reload_data", Context);
            }
            catch (RemoteMethodNotFoundException)
            {
                // Ignored
            }
            catch (ConnectionLostException)
            {
                // Ignored
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, $"Failed to call reload_data for plugin {Context.CurrentPluginMetadata.Name}", e);
            }
        }

        public virtual async ValueTask DisposeAsync()
        {
            try
            {
                await RPC.InvokeAsync("close");
            }
            catch (RemoteMethodNotFoundException)
            {
                // Ignored
            }
            catch (ConnectionLostException)
            {
                // Ignored
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, $"Failed to call close for plugin {Context.CurrentPluginMetadata.Name}", e);
            }
            finally
            {
                RPC?.Dispose();
                ErrorStream?.Dispose();
            }
        }
    }
}
