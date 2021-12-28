using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Flow.Launcher.Core.Plugin
{
    public abstract class JsonRPCPlugin2 : IAsyncPlugin, IResultUpdated, IAsyncDisposable
    {
        private PluginInitContext Context { get; set; }

        public const string JsonRPC = "JsonRPC";

        public abstract string SupportedLanguage { get; set; }

        protected abstract Task InitializeAsync(PluginInitContext context);
        protected Stream InputStream { get; set; }
        protected Stream OutputStream { get; set; }

        private SemaphoreSlim Locker { get; } = new(1, 1);

        private int _id;

        private readonly Channel<(Query query, List<Result> results)> _resultChannel = 
            Channel.CreateUnbounded<(Query, List<Result>)>();

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            await SendMessageAsync(new JsonRPCRequestModel
            {
                Id = _id++,
                Method = "query",
                Query = query
            });
            var (_, results) = await _resultChannel!.Reader.ReadAsync();
            return token.IsCancellationRequested ? null : results;
        }

        protected void LogErrorMessage(string message)
        {
            Log.Exception(GetType().Name, message, new Exception(message));
        }

        public async Task InitAsync(PluginInitContext context)
        {
            Context = context;
            await InitializeAsync(context);
            _ = Task.Run(ListenAsync);
            _ = Task.Run(InitializeResultUpdate);
        }

        private async Task InitializeResultUpdate()
        {
            var reader = _resultChannel.Reader;
            while (await reader.WaitToReadAsync())
            {
                await Task.Delay(20);
                if (reader.Count <= 0)
                {
                    continue;
                }
                
                var (query, results) = await reader.ReadAsync();
                ResultsUpdated?.Invoke(this, new()
                {
                    Query = query, Results = results
                });
            }
        }

        private JsonSerializerOptions Options { get; } = new()
        {
            PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly byte[] _delimiter = Encoding.UTF8.GetBytes("..**");

        private async Task ListenAsync()
        {
            while (true)
            {
                try
                {
                    var jsonBufferOwner = MemoryPool<byte>.Shared.Rent();
                    var jsonBuffer = jsonBufferOwner.Memory;
                    var position = 0;

                    using var bufferOwner = MemoryPool<byte>.Shared.Rent(512);
                    var buffer = bufferOwner.Memory;

                    while (OutputStream.CanRead)
                    {
                        var count = await OutputStream.ReadAsync(buffer);
                        var offset = 0;
                        var i = buffer.Span.IndexOf(_delimiter);
                        while (i != -1)
                        {
                            if (jsonBuffer.Length - position < i - offset)
                            {
                                ExpandBuffer();
                            }
                            buffer[offset..i].CopyTo(jsonBuffer[position..]);
                            var test = JsonSerializer.Deserialize<JsonRPCQueryResponseModel>
                                (jsonBuffer[..(position + i - offset)].Span, Options);
                            switch (test)
                            {
                                case { ActionType: JsonRPCResopnseActionType.Result } response:
                                    foreach (var jsonRpcResult in response.Result)
                                    {
                                        jsonRpcResult.Action += (context) => true;
                                    }
                                    await _resultChannel.Writer.WriteAsync((response.Query, response.Result.Cast<Result>().ToList()));
                                    break;
                                case { ActionType: JsonRPCResopnseActionType.RequestAPI } response:
                                    break;
                            }
                            offset = i + 4;
                            position = 0;
                            i = buffer[offset..].Span.IndexOf(_delimiter);
                        }
                        if (count + position > jsonBuffer.Length)
                        {
                            ExpandBuffer();
                        }

                        buffer[offset..count].CopyTo(jsonBuffer[position..]);
                        position += count - offset;

                        void ExpandBuffer()
                        {
                            var newLength = jsonBuffer.Length * 2;
                            var newOwner = MemoryPool<byte>.Shared.Rent(newLength);
                            var newMemory = newOwner.Memory;
                            jsonBuffer.CopyTo(newMemory);
                            jsonBufferOwner.Dispose();
                            jsonBufferOwner = newOwner;
                            jsonBuffer = jsonBufferOwner.Memory;
                        }
                    }
                }
                catch (Exception e)
                {

                }
                Log.Error(nameof(JsonRPCPlugin2), "Stream Not Readable");
            }


        }

        private async Task SendMessageAsync(JsonRPCRequestModel message)
        {
            await Locker.WaitAsync();
            await JsonSerializer.SerializeAsync(InputStream, message, Options);
            await InputStream.WriteAsync(_delimiter);
            Locker.Release();
        }



        public event ResultUpdatedEventHandler ResultsUpdated;

        public async ValueTask DisposeAsync()
        {
            await (InputStream?.DisposeAsync() ?? ValueTask.CompletedTask);
            await (OutputStream?.DisposeAsync() ?? ValueTask.CompletedTask);
        }
    }
}