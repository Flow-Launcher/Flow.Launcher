using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        protected abstract Task<string> ExecuteQueryAsync(Query query, CancellationToken token);
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
                Log.Exception($"|JsonRPCPlugin.LoadContextMenus|Exception on result <{selectedResult}>", e);
                return null;
            }
        }

        private List<Result> DeserializedResult(string output)
        {
            if (!String.IsNullOrEmpty(output))
            {
                List<Result> results = new List<Result>();

                JsonRPCQueryResponseModel queryResponseModel =
                    JsonSerializer.Deserialize<JsonRPCQueryResponseModel>(output);
                if (queryResponseModel.Result == null) return null;

                foreach (JsonRPCResult result in queryResponseModel.Result)
                {
                    result.Action = c =>
                    {
                        if (result.JsonRPCAction == null) return false;

                        if (!String.IsNullOrEmpty(result.JsonRPCAction.Method))
                        {
                            if (result.JsonRPCAction.Method.StartsWith("Flow.Launcher."))
                            {
                                ExecuteFlowLauncherAPI(result.JsonRPCAction.Method.Substring(4),
                                    result.JsonRPCAction.Parameters);
                            }
                            else
                            {
                                string actionReponse = ExecuteCallback(result.JsonRPCAction);
                                JsonRPCRequestModel jsonRpcRequestModel =
                                    JsonSerializer.Deserialize<JsonRPCRequestModel>(actionReponse);
                                if (jsonRpcRequestModel != null
                                    && !String.IsNullOrEmpty(jsonRpcRequestModel.Method)
                                    && jsonRpcRequestModel.Method.StartsWith("Flow.Launcher."))
                                {
                                    ExecuteFlowLauncherAPI(jsonRpcRequestModel.Method.Substring(4),
                                        jsonRpcRequestModel.Parameters);
                                }
                            }
                        }

                        return !result.JsonRPCAction.DontHideAfterAction;
                    };
                    results.Add(result);
                }

                return results;
            }

            return null;
        }

        private void ExecuteFlowLauncherAPI(string method, object[] parameters)
        {
            MethodInfo methodInfo = PluginManager.API.GetType().GetMethod(method);
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
        protected async Task<string> ExecuteAsync(string fileName, string arguments, CancellationToken token = default)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = fileName;
            start.Arguments = arguments;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            return await ExecuteAsync(start, token);
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
                    MessageBox.Show(new Form {TopMost = true}, result.Substring(6));
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

        protected async Task<string> ExecuteAsync(ProcessStartInfo startInfo, CancellationToken token = default)
        {
            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Log.Error("|JsonRPCPlugin.ExecuteAsync|Can't start new process");
                    return string.Empty;
                }

                using var standardOutput = process.StandardOutput;
                var result = await standardOutput.ReadToEndAsync();
                if (token.IsCancellationRequested)
                    return string.Empty;

                if (string.IsNullOrEmpty(result))
                {
                    using var standardError = process.StandardError;
                    var error = await standardError.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(error))
                    {
                        Log.Error($"|JsonRPCPlugin.ExecuteAsync|{error}");
                        return string.Empty;
                    }

                    Log.Error("|JsonRPCPlugin.ExecuteAsync|Empty standard output and standard error.");
                    return string.Empty;
                }

                if (result.StartsWith("DEBUG:"))
                {
                    MessageBox.Show(new Form {TopMost = true}, result.Substring(6));
                    return string.Empty;
                }

                return result;
            }
            catch (Exception e)
            {
                Log.Exception(
                    $"|JsonRPCPlugin.ExecuteAsync|Exception for filename <{startInfo.FileName}> with argument <{startInfo.Arguments}>",
                    e);
                return string.Empty;
            }
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            string output = await ExecuteQueryAsync(query, token);
            try
            {
                return DeserializedResult(output);
            }
            catch (Exception e)
            {
                Log.Exception($"|JsonRPCPlugin.Query|Exception when query <{query}>", e);
                return null;
            }
        }

        public Task InitAsync(PluginInitContext context)
        {
            this.context = context;
            return Task.CompletedTask;
        }
    }
}