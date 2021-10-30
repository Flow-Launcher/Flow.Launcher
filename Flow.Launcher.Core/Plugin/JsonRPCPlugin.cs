using Accessibility;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using ICSharpCode.SharpZipLib.Zip;
using JetBrains.Annotations;
using Microsoft.IO;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CheckBox = System.Windows.Controls.CheckBox;
using Control = System.Windows.Controls.Control;
using Label = System.Windows.Controls.Label;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace Flow.Launcher.Core.Plugin
{
    /// <summary>
    /// Represent the plugin that using JsonPRC
    /// every JsonRPC plugin should has its own plugin instance
    /// </summary>
    internal abstract class JsonRPCPlugin : IAsyncPlugin, IContextMenu, ISettingProvider, ISavable
    {
        protected PluginInitContext context;
        public const string JsonRPC = "JsonRPC";

        /// <summary>
        /// The language this JsonRPCPlugin support
        /// </summary>
        public abstract string SupportedLanguage { get; set; }
        protected abstract Task<Stream> RequestAsync(JsonRPCRequestModel rpcRequest, CancellationToken token = default);
        protected abstract string Request(JsonRPCRequestModel rpcRequest, CancellationToken token = default);

        private static readonly RecyclableMemoryStreamManager BufferManager = new();

        private string SettingConfigurationPath => Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "SettingsTemplate.yaml");
        private string SettingPath => Path.Combine(DataLocation.PluginSettingsDirectory, context.CurrentPluginMetadata.Name, "Settings.json");

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var request = new JsonRPCRequestModel
            {
                Method = "context_menu",
                Parameters = new[]
                {
                    selectedResult.ContextData
                }
            };
            var output = Request(request);
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
        private Dictionary<string, object> Settings { get; set; }

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
                        var actionResponse = Request(result.JsonRPCAction);

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
            var request = new JsonRPCRequestModel
            {
                Method = "query",
                Parameters = new[]
                {
                    query.Search
                }
            };
            var output = await RequestAsync(request, token);
            return await DeserializedResultAsync(output);
        }

        public async Task InitSettingAsync()
        {
            if (!File.Exists(SettingConfigurationPath))
                return;

            if (File.Exists(SettingPath))
                Settings = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(File.OpenRead(SettingPath), options);

            var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
            _settingsTemplate = deserializer.Deserialize<JsonRpcConfigurationModel>(await File.ReadAllTextAsync(SettingConfigurationPath));

            Settings ??= new Dictionary<string, object>();

            foreach (var (type, attribute) in _settingsTemplate.Body)
            {
                if (!Settings.ContainsKey(attribute.Name))
                {
                    Settings[attribute.Name] = attribute.DefaultValue;
                }
            }
        }

        public virtual async Task InitAsync(PluginInitContext context)
        {
            this.context = context;
            await InitSettingAsync();
        }
        private static readonly Thickness settingControlMargin = new(10);
        private JsonRpcConfigurationModel _settingsTemplate;
        public Control CreateSettingPanel()
        {
            if (Settings == null)
                return new();
            var settingWindow = new UserControl();
            var mainPanel = new StackPanel
            {
                Margin = settingControlMargin, Orientation = Orientation.Vertical
            };
            settingWindow.Content = mainPanel;

            foreach (var (type, attribute) in _settingsTemplate.Body)
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = settingControlMargin
                };
                var name = new Label()
                {
                    Content = attribute.Label,
                    Margin = settingControlMargin
                };

                Control contentControl;

                switch (type)
                {
                    case "Input":
                        {
                            var textBox = new TextBox()
                            {
                                Width = 300, Text = Settings[attribute.Name] as string ?? string.Empty,
                                Margin = settingControlMargin
                            };
                            textBox.TextChanged += (_, _) =>
                            {
                                Settings[attribute.Name] = textBox.Text;
                            };
                            contentControl = textBox;
                            break;
                        }
                    case "textarea":
                        {
                            var textBox = new TextBox()
                            {
                                Width = 300,
                                Height = 100,
                                Margin = settingControlMargin,
                                TextWrapping = TextWrapping.WrapWithOverflow,
                                Text = Settings[attribute.Name] as string ?? string.Empty
                            };
                            textBox.TextChanged += (sender, _) =>
                            {
                                Settings[attribute.Name] = ((TextBox)sender).Text;
                            };
                            contentControl = textBox;
                            break;
                        }
                    case "dropdown":
                        {
                            var comboBox = new ComboBox()
                            {
                                ItemsSource = attribute.Options, SelectedItem = Settings[attribute.Name],
                                Margin = settingControlMargin
                            };
                            comboBox.SelectionChanged += (sender, _) =>
                            {
                                Settings[attribute.Name] = (string)((ComboBox)sender).SelectedItem;
                            };
                            contentControl = comboBox;
                            break;
                        }
                    case "checkbox":
                        var checkBox = new CheckBox
                        {
                            IsChecked = Settings[attribute.Name] is bool isChecked ? isChecked : bool.Parse(attribute.DefaultValue),
                            Margin = settingControlMargin
                        };
                        checkBox.Click += (sender, _) =>
                        {
                            Settings[attribute.Name] = ((CheckBox) sender).IsChecked;
                        };
                        contentControl = checkBox;
                        break;
                    default:
                        continue;
                }
                panel.Children.Add(name);
                panel.Children.Add(contentControl);
                mainPanel.Children.Add(panel);
            }

            // foreach (var (key, value) in Settings)
            // {
            //     var panel = new StackPanel
            //     {
            //         Orientation = Orientation.Horizontal, Margin = settingControlMargin
            //     };
            //     var name = new Label
            //     {
            //         Content = key, VerticalAlignment = VerticalAlignment.Center
            //     };
            //     UIElement content = null;
            //     switch (value)
            //     {
            //         case int i:
            //         case double d:
            //             throw new TypeAccessException();
            //         case string s:
            //             var textBox = new TextBox
            //             {
            //                 Text = s,
            //                 Margin = settingControlMargin,
            //                 VerticalAlignment = VerticalAlignment.Center
            //             };
            //             textBox.TextChanged += (_, _) =>
            //             {
            //                 Settings[key] = textBox.Text;
            //             };
            //             content = textBox;
            //             break;
            //         case bool b:
            //             var checkBox = new CheckBox
            //             {
            //                 IsChecked = b,
            //                 Margin = settingControlMargin,
            //                 VerticalAlignment = VerticalAlignment.Center
            //             };
            //             checkBox.Click += (_, _) =>
            //             {
            //                 Settings[key] = checkBox.IsChecked;
            //             };
            //             content = checkBox;
            //             break;
            //         default:
            //             throw new ArgumentOutOfRangeException();
            //     }
            //     
            // }
            return settingWindow;
        }
        public void Save()
        {
            if (Settings != null)
            {
                Helper.ValidateDirectory(Path.Combine(DataLocation.PluginSettingsDirectory, context.CurrentPluginMetadata.Name));
                File.WriteAllText(SettingPath, JsonSerializer.Serialize(Settings));
            }
        }
    }
}