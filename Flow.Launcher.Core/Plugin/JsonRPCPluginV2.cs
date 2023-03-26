using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public abstract class JsonRpcPluginV2 : IAsyncPlugin, IContextMenu, ISettingProvider, ISavable
    {
        public abstract string SupportedLanguage { get; set; }
        
        public const string JsonRpc = "JsonRPC";
        protected abstract Stream InputStream { get; set; }
        protected abstract Stream OutputStream { get; set; }
        protected abstract StreamReader ErrorStream { get; set; }

        protected Channel<JsonRPCRequestModel> InputMessageChannel { get; set; }

        private (Task SendTask, Task ReceiveTask) MessageTask { get; set; }
        private CancellationTokenSource MessageCancellationTokenSource { get; set; }

        protected int RequestId;

        private ConcurrentDictionary<int, TaskCompletionSource<JsonRPCQueryResponseModel>> RequestTaskDictionary { get; } = new();

        // TODO: Switch to Async Task
        private async void ReceiveMessageAsync(CancellationToken token)
        {
            var response = 
                JsonSerializer.DeserializeAsyncEnumerable<JsonRPCQueryResponseModel>(OutputStream, cancellationToken: token);

            ArgumentNullException.ThrowIfNull(response);

            await foreach (var message in response.WithCancellation(token))
            {
                if (!RequestTaskDictionary.TryGetValue(message.Id, out var task))
                {
                    // Either Task is already handled or it is a invalid resopnse.
                    continue;
                }
                RequestTaskDictionary.Remove(message.Id, out _);
                task.TrySetResult(message);
            }
        }

        // TODO: Switch to Async Task
        private async void SendMessageAsync(PluginMetadata metadata, CancellationToken token)
        {
            var fullMessage = new JsonRPCRequestMessage(metadata, InputMessageChannel.Reader.ReadAllAsync(token));
            await JsonSerializer.SerializeAsync(InputStream, fullMessage, cancellationToken: token);
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            int currentRequestId = Interlocked.Add(ref RequestId, 1);
            var message = new JsonRPCRequestModel(currentRequestId, "query", new object[]
            {
                query
            });
            await InputMessageChannel.Writer.WriteAsync(message, token);
            await Task.Delay(50, token);
            await InputStream.FlushAsync(token);
            var task = new TaskCompletionSource<JsonRPCQueryResponseModel>();
            RequestTaskDictionary[currentRequestId] = task;
            var result = await task.Task;
            //TODO: Parse Result
            return new List<Result>();
        }
        public virtual Task InitAsync(PluginInitContext context)
        {
            InputMessageChannel = Channel.CreateUnbounded<JsonRPCRequestModel>();
            MessageCancellationTokenSource = new CancellationTokenSource();
            SendMessageAsync(context.CurrentPluginMetadata, MessageCancellationTokenSource.Token);
            ReceiveMessageAsync(MessageCancellationTokenSource.Token);
            // MessageTask = 
            //     (SendMessageAsync(context.CurrentPluginMetadata, MessageCancellationTokenSource.Token),
            //     ReceiveMessageAsync(MessageCancellationTokenSource.Token));
            return Task.CompletedTask;
        }
        public List<Result> LoadContextMenus(Result selectedResult)
        {
            throw new System.NotImplementedException();
        }
        public Control CreateSettingPanel()
        {
            // TODO: Implement CreateSettingPanel
            return new Control();
        }
        public void Save()
        {
            // TODO: Save settings
        }
    }
}
