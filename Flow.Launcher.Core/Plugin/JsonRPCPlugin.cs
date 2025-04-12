using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin;
using Microsoft.IO;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Represent the plugin that using JsonPRC
    /// every JsonRPC plugin should has its own plugin instance
    /// </summary>
    internal abstract class JsonRPCPlugin : JsonRPCPluginBase
    {
        public new const string JsonRPC = "JsonRPC";

        private static readonly string ClassName = nameof(JsonRPCPlugin);

        protected abstract Task<Stream> RequestAsync(JsonRPCRequestModel rpcRequest, CancellationToken token = default);
        protected abstract string Request(JsonRPCRequestModel rpcRequest, CancellationToken token = default);

        private static readonly RecyclableMemoryStreamManager BufferManager = new();

        private int RequestId { get; set; }

        public override List<Result> LoadContextMenus(Result selectedResult)
        {
            var request = new JsonRPCRequestModel(RequestId++, 
                "context_menu", 
                new[] { selectedResult.ContextData });
            var output = Request(request);
            return DeserializedResult(output);
        }

        private static readonly JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
#pragma warning disable SYSLIB0020
            // IgnoreNullValues is obsolete, but the replacement JsonIgnoreCondition.WhenWritingNull still 
            // deserializes null, instead of ignoring it and leaving the default (empty list). We can change the behaviour
            // to accept null and fallback to a default etc, or just keep IgnoreNullValues for now
            // see: https://github.com/dotnet/runtime/issues/39152
            IgnoreNullValues = true,
#pragma warning restore SYSLIB0020 // Type or member is obsolete
            Converters =
            {
                new JsonObjectConverter()
            }
        };

        private async Task<List<Result>> DeserializedResultAsync(Stream output)
        {
            await using (output)
            {
                if (output == Stream.Null) return null;

                var queryResponseModel =
                    await JsonSerializer.DeserializeAsync<JsonRPCQueryResponseModel>(output, options);

                return ParseResults(queryResponseModel);
            }
        }

        private List<Result> DeserializedResult(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;

            var queryResponseModel =
                JsonSerializer.Deserialize<JsonRPCQueryResponseModel>(output, options);
            return ParseResults(queryResponseModel);
        }

        protected override async Task<bool> ExecuteResultAsync(JsonRPCResult result)
        {
            if (result.JsonRPCAction == null) return false;

            if (string.IsNullOrEmpty(result.JsonRPCAction.Method))
            {
                return !result.JsonRPCAction.DontHideAfterAction;
            }

            if (result.JsonRPCAction.Method.StartsWith("Flow.Launcher."))
            {
                ExecuteFlowLauncherAPI(result.JsonRPCAction.Method["Flow.Launcher.".Length..],
                    result.JsonRPCAction.Parameters);
            }
            else
            {
                await using var actionResponse = await RequestAsync(result.JsonRPCAction);

                if (actionResponse.Length == 0)
                {
                    return !result.JsonRPCAction.DontHideAfterAction;
                }

                var jsonRpcRequestModel = await
                    JsonSerializer.DeserializeAsync<JsonRPCRequestModel>(actionResponse, options);

                if (jsonRpcRequestModel?.Method?.StartsWith("Flow.Launcher.") ?? false)
                {
                    ExecuteFlowLauncherAPI(jsonRpcRequestModel.Method["Flow.Launcher.".Length..],
                        jsonRpcRequestModel.Parameters);
                }
            }

            return !result.JsonRPCAction.DontHideAfterAction;
        }

        /// <summary>
        /// Execute external program and return the output
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="arguments"></param>
        /// <param name="token">Cancellation Token</param>
        /// <returns></returns>
        protected Task<Stream> ExecuteAsync(string fileName, string arguments, CancellationToken token = default)
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            return ExecuteAsync(start, token);
        }

        protected string Execute(ProcessStartInfo startInfo)
        {
            try
            {
                using var process = Process.Start(startInfo);
                if (process == null) return string.Empty;

                using var standardOutput = process.StandardOutput;
                var result = standardOutput.ReadToEnd();

                if (string.IsNullOrEmpty(result))
                {
                    using var standardError = process.StandardError;
                    var error = standardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(error))
                    {
                        Context.API.LogError(ClassName, error);
                        return string.Empty;
                    }

                    Context.API.LogError(ClassName, "Empty standard output and standard error.");
                    return string.Empty;
                }

                return result;
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName,
                    $"Exception for filename <{startInfo.FileName}> with argument <{startInfo.Arguments}>",
                    e);
                return string.Empty;
            }
        }

        protected async Task<Stream> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken token = default)
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Context.API.LogError(ClassName, "Can't start new process");
                return Stream.Null;
            }

            var sourceBuffer = BufferManager.GetStream();
            using var errorBuffer = BufferManager.GetStream();

            var sourceCopyTask = process.StandardOutput.BaseStream.CopyToAsync(sourceBuffer, token);
            var errorCopyTask = process.StandardError.BaseStream.CopyToAsync(errorBuffer, token);

            await using var registeredEvent = token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                    sourceBuffer.Dispose();
                }
                catch (Exception e)
                {
                    Context.API.LogException(ClassName, "Exception when kill process", e);
                }
            });

            try
            {
                // token expire won't instantly trigger the exception, 
                // manually kill process at before
                await process.WaitForExitAsync(token);
                await Task.WhenAll(sourceCopyTask, errorCopyTask);
            }
            catch (OperationCanceledException)
            {
                await sourceBuffer.DisposeAsync();
                return Stream.Null;
            }

            switch (sourceBuffer.Length, errorBuffer.Length)
            {
                case (0, 0):
                    const string errorMessage = "Empty JSON-RPC Response.";
                    Context.API.LogWarn(ClassName, errorMessage);
                    break;
                case (_, not 0):
                    throw new InvalidDataException(Encoding.UTF8.GetString(errorBuffer.ToArray())); // The process has exited with an error message
            }

            sourceBuffer.Seek(0, SeekOrigin.Begin);

            return sourceBuffer;
        }

        public override async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var request = new JsonRPCRequestModel(RequestId++,
                "query",
                new object[]
                {
                    query.Search
                },
                Settings?.Inner);

            var output = await RequestAsync(request, token);

            return await DeserializedResultAsync(output);
        }
    }
}
