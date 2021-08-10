using Flow.Launcher.Core.Resource;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin;
using ICSharpCode.SharpZipLib.Zip;
using JetBrains.Annotations;
using Microsoft.IO;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Represent the plugin that using JsonPRC
    /// every JsonRPC plugin should has its own plugin instance
    /// </summary>
    internal abstract class JsonRPCPlugin : IAsyncPlugin, IContextMenu
    {
        protected PluginInitContext context;
        public const string JsonRPC = "JsonRPC";

        /// <summary>
        /// The language this JsonRPCPlugin support
        /// </summary>
        public abstract string SupportedLanguage { get; set; }

        protected abstract Task<Stream> ExecuteQueryAsync(Query query, CancellationToken token);
        protected abstract string ExecuteCallback(JsonRPCRequestModel rpcRequest);
        protected abstract string ExecuteContextMenu(Result selectedResult);

        private static readonly RecyclableMemoryStreamManager BufferManager = new();

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var output = ExecuteContextMenu(selectedResult);
            return DeserializedResult(output);
        }

        private static readonly JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            IgnoreNullValues = true,
            Converters =
            {
                new JsonObjectConverter()
            }
        };

        private async Task<List<Result>> DeserializedResultAsync(Stream output)
        {
            if (output == Stream.Null) return null;

            var queryResponseModel =
                await JsonSerializer.DeserializeAsync<JsonRPCQueryResponseModel>(output, options);

            return ParseResults(queryResponseModel);
        }

        private List<Result> DeserializedResult(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;

            var queryResponseModel =
                JsonSerializer.Deserialize<JsonRPCQueryResponseModel>(output, options);
            return ParseResults(queryResponseModel);
        }


        private List<Result> ParseResults(JsonRPCQueryResponseModel queryResponseModel)
        {
            if (queryResponseModel.Result == null) return null;

            if (!string.IsNullOrEmpty(queryResponseModel.DebugMessage))
            {
                context.API.ShowMsg(queryResponseModel.DebugMessage);
            }

            foreach (var result in queryResponseModel.Result)
            {
                result.Action = c =>
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
                        var actionResponse = ExecuteCallback(result.JsonRPCAction);

                        if (string.IsNullOrEmpty(actionResponse))
                        {
                            return !result.JsonRPCAction.DontHideAfterAction;
                        }

                        var jsonRpcRequestModel =
                            JsonSerializer.Deserialize<JsonRPCRequestModel>(actionResponse, options);

                        if (jsonRpcRequestModel?.Method?.StartsWith("Flow.Launcher.") ?? false)
                        {
                            ExecuteFlowLauncherAPI(jsonRpcRequestModel.Method["Flow.Launcher.".Length..],
                                jsonRpcRequestModel.Parameters);
                        }
                    }

                    return !result.JsonRPCAction.DontHideAfterAction;
                };
            }

            var results = new List<Result>();

            results.AddRange(queryResponseModel.Result);

            return results;
        }

        private void ExecuteFlowLauncherAPI(string method, object[] parameters)
        {
            var parametersTypeArray = parameters.Select(param => param.GetType()).ToArray();
            MethodInfo methodInfo = PluginManager.API.GetType().GetMethod(method, parametersTypeArray);
            if (methodInfo != null)
            {
                try
                {
                    methodInfo.Invoke(PluginManager.API, parameters);
                }
                catch (Exception)
                {
#if (DEBUG)
                    throw;
#endif
                }
            }
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
                        Log.Error($"|JsonRPCPlugin.Execute|{error}");
                        return string.Empty;
                    }

                    Log.Error("|JsonRPCPlugin.Execute|Empty standard output and standard error.");
                    return string.Empty;
                }

                if (result.StartsWith("DEBUG:"))
                {
                    MessageBox.Show(new Form
                    {
                        TopMost = true
                    }, result.Substring(6));
                    return string.Empty;
                }

                return result;
            }
            catch (Exception e)
            {
                Log.Exception(
                    $"|JsonRPCPlugin.Execute|Exception for filename <{startInfo.FileName}> with argument <{startInfo.Arguments}>",
                    e);
                return string.Empty;
            }
        }

        protected async Task<Stream> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken token = default)
        {
            Process process = null;
            bool disposed = false;
            try
            {
                process = Process.Start(startInfo);
                if (process == null)
                {
                    Log.Error("|JsonRPCPlugin.ExecuteAsync|Can't start new process");
                    return Stream.Null;
                }

                await using var source = process.StandardOutput.BaseStream;

                var buffer = BufferManager.GetStream();

                token.Register(() =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    // Manually Check whether disposed
                    if (!disposed && !process.HasExited)
                        process.Kill();
                });

                try
                {
                    // token expire won't instantly trigger the exception, 
                    // manually kill process at before
                    await source.CopyToAsync(buffer, token);
                }
                catch (OperationCanceledException)
                {
                    await buffer.DisposeAsync();
                    return Stream.Null;
                }

                buffer.Seek(0, SeekOrigin.Begin);

                token.ThrowIfCancellationRequested();

                if (buffer.Length == 0)
                {
                    var errorMessage = process.StandardError.EndOfStream ? 
                        "Empty JSONRPC Response" : 
                        await process.StandardError.ReadToEndAsync();
                    throw new InvalidDataException($"{context.CurrentPluginMetadata.Name}|{errorMessage}");
                }

                if (!process.StandardError.EndOfStream)
                {
                    using var standardError = process.StandardError;
                    var error = await standardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Log.Error($"|{context.CurrentPluginMetadata.Name}.{nameof(ExecuteAsync)}|{error}");
                    }
                }

                return buffer;
            }
            finally
            {
                process?.Dispose();
                disposed = true;
            }
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var output = await ExecuteQueryAsync(query, token);
            return await DeserializedResultAsync(output);
        }

        public virtual Task InitAsync(PluginInitContext context)
        {
            this.context = context;
            return Task.CompletedTask;
        }
    }
}
