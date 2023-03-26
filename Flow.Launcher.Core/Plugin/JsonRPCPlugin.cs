using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Microsoft.IO;
using System.Windows;
using System.Windows.Controls;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CheckBox = System.Windows.Controls.CheckBox;
using Control = System.Windows.Controls.Control;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Documents;
using static System.Windows.Forms.LinkLabel;
using Droplex;
using System.Windows.Forms;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Represent the plugin that using JsonPRC
    /// every JsonRPC plugin should has its own plugin instance
    /// </summary>
    internal abstract class JsonRPCPlugin : JsonRPCPluginBase
    {
        public const string JsonRPC = "JsonRPC";

        protected abstract Task<Stream> RequestAsync(JsonRPCRequestModel rpcRequest, CancellationToken token = default);
        protected abstract string Request(JsonRPCRequestModel rpcRequest, CancellationToken token = default);

        private static readonly RecyclableMemoryStreamManager BufferManager = new();

        private int RequestId { get; set; }

        private string SettingConfigurationPath => Path.Combine(Context.CurrentPluginMetadata.PluginDirectory, "SettingsTemplate.yaml");
        private string SettingPath => Path.Combine(DataLocation.PluginSettingsDirectory, Context.CurrentPluginMetadata.Name, "Settings.json");

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

        private static readonly JsonSerializerOptions settingSerializeOption = new()
        {
            WriteIndented = true
        };

        private readonly Dictionary<string, FrameworkElement> _settingControls = new();

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
                        Log.Error($"|JsonRPCPlugin.Execute|{error}");
                        return string.Empty;
                    }

                    Log.Error("|JsonRPCPlugin.Execute|Empty standard output and standard error.");
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
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Log.Error("|JsonRPCPlugin.ExecuteAsync|Can't start new process");
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
                    Log.Exception("|JsonRPCPlugin.ExecuteAsync|Exception when kill process", e);
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
                    Log.Warn($"|{nameof(JsonRPCPlugin)}.{nameof(ExecuteAsync)}|{errorMessage}");
                    break;
                case (_, not 0):
                    throw new InvalidDataException(Encoding.UTF8.GetString(errorBuffer.ToArray())); // The process has exited with an error message
            }

            sourceBuffer.Seek(0, SeekOrigin.Begin);

            return sourceBuffer;
        }


        protected override async Task<List<Result>> QueryRequestAsync(JsonRPCRequestModel request, CancellationToken token)
        {
            var output = await RequestAsync(request, token);
            return await DeserializedResultAsync(output);
        }
    }
}
