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
using JetBrains.Annotations;

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

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            string output = ExecuteContextMenu(selectedResult);
            try
            {
                return DeserializedResult(output);
            }
            catch (Exception e)
            {
                Log.Exception(nameof(JsonRPCPlugin), $"Exception on result <{selectedResult}>", e);
                return null;
            }
        }

        private static readonly JsonSerializerOptions _options = new()
        {
            Converters =
            {
                new JsonObjectConverter()
            }
        };

        private async Task<List<Result>> DeserializedResultAsync(Stream output)
        {
            if (output == Stream.Null) return null;

            var queryResponseModel = await
                JsonSerializer.DeserializeAsync<JsonRPCQueryResponseModel>(output, _options);

            return ParseResults(queryResponseModel);
        }

        private List<Result> DeserializedResult(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;

            var queryResponseModel =
                JsonSerializer.Deserialize<JsonRPCQueryResponseModel>(output, _options);
            return ParseResults(queryResponseModel);
        }


        private List<Result> ParseResults(JsonRPCQueryResponseModel queryResponseModel)
        {
            var results = new List<Result>();
            if (queryResponseModel.Result == null) return null;

            if (!string.IsNullOrEmpty(queryResponseModel.DebugMessage))
            {
                context.API.ShowMsg(queryResponseModel.DebugMessage);
            }

            foreach (JsonRPCResult result in queryResponseModel.Result)
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

                        var jsonRpcRequestModel = JsonSerializer.Deserialize<JsonRPCRequestModel>(actionResponse, _options);

                        if (jsonRpcRequestModel?.Method?.StartsWith("Flow.Launcher.") ?? false)
                        {
                            ExecuteFlowLauncherAPI(jsonRpcRequestModel.Method["Flow.Launcher.".Length..],
                                jsonRpcRequestModel.Parameters);
                        }
                    }

                    return !result.JsonRPCAction.DontHideAfterAction;
                };
                results.Add(result);
            }

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
                        Log.Error("JsonRPCPlugin", $"{error}");
                        return string.Empty;
                    }

                    Log.Error("JsonRPCPlugin", "Empty standard output and standard error.");
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
                Log.Exception(nameof(JsonRPCPlugin),
                    $"Exception for filename <{startInfo.FileName}> with argument <{startInfo.Arguments}>", e);
                return string.Empty;
            }
        }

        protected async Task<Stream> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken token = default)
        {
            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Log.Error("JsonRPCPlugin", "Can't start new process");
                    return Stream.Null;
                }

                var result = process.StandardOutput.BaseStream;

                token.ThrowIfCancellationRequested();

                if (!process.StandardError.EndOfStream)
                {
                    using var standardError = process.StandardError;
                    var error = await standardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Log.Error("JsonRPCPlugin", $"{error}");
                        return Stream.Null;
                    }

                    Log.Error("JsonRPCPlugin", "Empty standard output and standard error.");
                    return Stream.Null;
                }

                return result;
            }
            catch (Exception e)
            {
                Log.Exception(
                    "JsonRPCPlugin",
                    $"Exception for filename <{startInfo.FileName}> with argument <{startInfo.Arguments}>",
                    e);
                return Stream.Null;
            }
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var output = await ExecuteQueryAsync(query, token);
            try
            {
                return await DeserializedResultAsync(output);
            }
            catch (Exception e)
            {
                Log.Exception(nameof(JsonRPCPlugin),$"Exception when query <{query}>", e);
                return null;
            }
        }

        public virtual Task InitAsync(PluginInitContext context)
        {
            this.context = context;
            return Task.CompletedTask;
        }
    }
}