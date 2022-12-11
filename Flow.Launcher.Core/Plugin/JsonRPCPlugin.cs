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
        private Dictionary<string, object> Settings { get; set; }

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


        private List<Result> ParseResults(JsonRPCQueryResponseModel queryResponseModel)
        {
            if (queryResponseModel.Result == null) return null;

            if (!string.IsNullOrEmpty(queryResponseModel.DebugMessage))
            {
                context.API.ShowMsg(queryResponseModel.DebugMessage);
            }

            foreach (var result in queryResponseModel.Result)
            {
                result.AsyncAction = async c =>
                {
                    UpdateSettings(result.SettingsChange);

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
                };
            }

            var results = new List<Result>();

            results.AddRange(queryResponseModel.Result);

            UpdateSettings(queryResponseModel.SettingsChange);

            return results;
        }

        private void ExecuteFlowLauncherAPI(string method, object[] parameters)
        {
            var parametersTypeArray = parameters.Select(param => param.GetType()).ToArray();
            var methodInfo = typeof(IPublicAPI).GetMethod(method, parametersTypeArray);
            if (methodInfo == null)
            {
                return;
            }
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
                if (!process.HasExited)
                    process.Kill();
                sourceBuffer.Dispose();
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


        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var request = new JsonRPCRequestModel
            {
                Method = "query",
                Parameters = new object[]
                {
                    query.Search
                },
                Settings = Settings
            };
            var output = await RequestAsync(request, token);
            return await DeserializedResultAsync(output);
        }

        public async Task InitSettingAsync()
        {
            if (!File.Exists(SettingConfigurationPath))
                return;

            if (File.Exists(SettingPath))
            {
                await using var fileStream = File.OpenRead(SettingPath);
                Settings = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(fileStream, options);
            }

            var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
            _settingsTemplate = deserializer.Deserialize<JsonRpcConfigurationModel>(await File.ReadAllTextAsync(SettingConfigurationPath));

            Settings ??= new Dictionary<string, object>();

            foreach (var (type, attribute) in _settingsTemplate.Body)
            {
                if (type == "textBlock")
                    continue;
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
        private static readonly Thickness settingControlMargin = new(0, 9, 18, 9);
        private static readonly Thickness settingCheckboxMargin = new(0, 9, 9, 9);
        private static readonly Thickness settingPanelMargin = new(0, 0, 0, 0);
        private static readonly Thickness settingTextBlockMargin = new(70, 9, 18, 9);
        private static readonly Thickness settingLabelPanelMargin = new(70, 9, 18, 9);
        private static readonly Thickness settingLabelMargin = new(0, 0, 0, 0);
        private static readonly Thickness settingDescMargin = new(0, 2, 0, 0);
        private static readonly Thickness settingSepMargin = new(0, 0, 0, 2);
        private JsonRpcConfigurationModel _settingsTemplate;

        public Control CreateSettingPanel()
        {
            if (Settings == null)
                return new();
            var settingWindow = new UserControl();
            var mainPanel = new Grid
            {
                Margin = settingPanelMargin, VerticalAlignment = VerticalAlignment.Center
            };
            ColumnDefinition gridCol1 = new ColumnDefinition();
            ColumnDefinition gridCol2 = new ColumnDefinition();

            gridCol1.Width = new GridLength(70, GridUnitType.Star);
            gridCol2.Width = new GridLength(30, GridUnitType.Star);
            mainPanel.ColumnDefinitions.Add(gridCol1);
            mainPanel.ColumnDefinitions.Add(gridCol2);
            settingWindow.Content = mainPanel;
            int rowCount = 0;
            foreach (var (type, attribute) in _settingsTemplate.Body)
            {
                Separator sep = new Separator();
                sep.VerticalAlignment = VerticalAlignment.Top;
                sep.Margin = settingSepMargin;
                sep.SetResourceReference(Separator.BackgroundProperty, "Color03B"); /* for theme change */
                var panel = new StackPanel
                {
                    Background = System.Windows.SystemColors.GrayTextBrush,
                    Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center,
                    Margin = settingLabelPanelMargin
                };
                RowDefinition gridRow = new RowDefinition();
                mainPanel.RowDefinitions.Add(gridRow);
                var name = new TextBlock()
                {
                    Text = attribute.Label,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = settingLabelMargin,
                    TextWrapping = TextWrapping.WrapWithOverflow
                };
                var desc = new TextBlock()
                {
                    Text = attribute.Description, FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,Margin = settingDescMargin,
                    TextWrapping = TextWrapping.WrapWithOverflow
                };
                desc.SetResourceReference(TextBlock.ForegroundProperty, "Color04B");

                if (attribute.Description == null) /* if no description, hide */
                    desc.Visibility = Visibility.Collapsed;


                if (type != "textBlock") /* if textBlock, hide desc */
                {
                    panel.Children.Add(name);
                    panel.Children.Add(desc);
                }
                    

                Grid.SetColumn(panel, 0);
                Grid.SetRow(panel, rowCount);

                FrameworkElement contentControl;

                switch (type)
                {
                    case "textBlock":
                    {
                        contentControl = new TextBlock
                        {
                            Text = attribute.Description.Replace("\\r\\n", "\r\n"),
                            Margin = settingTextBlockMargin,
                            Padding = new Thickness(0,0,0,0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            TextAlignment = TextAlignment.Left,
                            TextWrapping = TextWrapping.Wrap
                        };
                            Grid.SetColumn(contentControl, 0);
                            Grid.SetColumnSpan(contentControl, 2);
                            Grid.SetRow(contentControl, rowCount);
                            if (rowCount != 0)
                                mainPanel.Children.Add(sep);
                            Grid.SetRow(sep, rowCount);
                            Grid.SetColumn(sep, 0);
                            Grid.SetColumnSpan(sep, 2);
                            break;
                    }
                    case "input":
                    {
                        var textBox = new TextBox()
                        {
                            Text = Settings[attribute.Name] as string ?? string.Empty,
                            Margin = settingControlMargin,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            ToolTip = attribute.Description
                        };
                        textBox.TextChanged += (_, _) =>
                        {
                            Settings[attribute.Name] = textBox.Text;
                        };
                        contentControl = textBox;
                            Grid.SetColumn(contentControl, 1);
                            Grid.SetRow(contentControl, rowCount);
                            if (rowCount != 0)
                                mainPanel.Children.Add(sep);
                            Grid.SetRow(sep, rowCount);
                            Grid.SetColumn(sep, 0);
                            Grid.SetColumnSpan(sep, 2);
                            break;
                    }
                    case "textarea":
                    {
                        var textBox = new TextBox()
                        {
                            Height = 120,
                            Margin = settingControlMargin,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.WrapWithOverflow,
                            AcceptsReturn = true,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Text = Settings[attribute.Name] as string ?? string.Empty,
                            ToolTip = attribute.Description
                        };
                        textBox.TextChanged += (sender, _) =>
                        {
                            Settings[attribute.Name] = ((TextBox)sender).Text;
                        };
                        contentControl = textBox;
                            Grid.SetColumn(contentControl, 1);
                            Grid.SetRow(contentControl, rowCount);
                            if (rowCount != 0)
                                mainPanel.Children.Add(sep);
                            Grid.SetRow(sep, rowCount);
                            Grid.SetColumn(sep, 0);
                            Grid.SetColumnSpan(sep, 2);
                            break;
                    }
                    case "passwordBox":
                    {
                        var passwordBox = new PasswordBox()
                        {
                            Margin = settingControlMargin,
                            Password = Settings[attribute.Name] as string ?? string.Empty,
                            PasswordChar = attribute.passwordChar == default ? '*' : attribute.passwordChar,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            ToolTip = attribute.Description
                        };
                        passwordBox.PasswordChanged += (sender, _) =>
                        {
                            Settings[attribute.Name] = ((PasswordBox)sender).Password;
                        };
                        contentControl = passwordBox;
                            Grid.SetColumn(contentControl, 1);
                            Grid.SetRow(contentControl, rowCount);
                            if (rowCount != 0)
                                mainPanel.Children.Add(sep);
                            Grid.SetRow(sep, rowCount);
                            Grid.SetColumn(sep, 0);
                            Grid.SetColumnSpan(sep, 2);
                            break;
                    }
                    case "dropdown":
                    {
                        var comboBox = new ComboBox()
                        {
                            ItemsSource = attribute.Options,
                            SelectedItem = Settings[attribute.Name],
                            Margin = settingControlMargin,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            ToolTip = attribute.Description
                        };
                        comboBox.SelectionChanged += (sender, _) =>
                        {
                            Settings[attribute.Name] = (string)((ComboBox)sender).SelectedItem;
                        };
                        contentControl = comboBox;
                            Grid.SetColumn(contentControl, 1);
                            Grid.SetRow(contentControl, rowCount);
                            if (rowCount != 0)
                                mainPanel.Children.Add(sep);
                            Grid.SetRow(sep, rowCount);
                            Grid.SetColumn(sep, 0);
                            Grid.SetColumnSpan(sep, 2);
                            break;
                    }
                    case "checkbox":
                        var checkBox = new CheckBox
                        {
                            IsChecked = Settings[attribute.Name] is bool isChecked ? isChecked : bool.Parse(attribute.DefaultValue),
                            Margin = settingCheckboxMargin,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            ToolTip = attribute.Description
                        };
                        checkBox.Click += (sender, _) =>
                        {
                            Settings[attribute.Name] = ((CheckBox)sender).IsChecked;
                        };
                        contentControl = checkBox;
                        Grid.SetColumn(contentControl, 1);
                        Grid.SetRow(contentControl, rowCount);
                        if (rowCount != 0)
                            mainPanel.Children.Add(sep);
                        Grid.SetRow(sep, rowCount);
                        Grid.SetColumn(sep, 0);
                        Grid.SetColumnSpan(sep, 2);
                        break;
                    case "hyperlink":
                        var hyperlink = new Hyperlink
                        {
                            ToolTip = attribute.Description,
                            NavigateUri = attribute.url
                        };
                        var linkbtn = new Button
                        {
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = settingControlMargin
                        };
                        linkbtn.Content = attribute.urlLabel;

                        contentControl = linkbtn;
                        Grid.SetColumn(contentControl, 1);
                        Grid.SetRow(contentControl, rowCount);
                        if (rowCount != 0)
                            mainPanel.Children.Add(sep);
                        Grid.SetRow(sep, rowCount);
                        Grid.SetColumn(sep, 0);
                        Grid.SetColumnSpan(sep, 2);
                        break;
                    default:
                        continue;
                }
                if (type != "textBlock")
                    _settingControls[attribute.Name] = contentControl;
                mainPanel.Children.Add(panel);
                mainPanel.Children.Add(contentControl);
                rowCount++;
                
            }
            return settingWindow;
        }

        public void Save()
        {
            if (Settings != null)
            {
                Helper.ValidateDirectory(Path.Combine(DataLocation.PluginSettingsDirectory, context.CurrentPluginMetadata.Name));
                File.WriteAllText(SettingPath, JsonSerializer.Serialize(Settings, settingSerializeOption));
            }
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            if (settings == null || settings.Count == 0)
                return;

            foreach (var (key, value) in settings)
            {
                if (Settings.ContainsKey(key))
                {
                    Settings[key] = value;
                }
                if (_settingControls.ContainsKey(key))
                {

                    switch (_settingControls[key])
                    {
                        case TextBox textBox:
                            textBox.Dispatcher.Invoke(() => textBox.Text = value as string);
                            break;
                        case PasswordBox passwordBox:
                            passwordBox.Dispatcher.Invoke(() => passwordBox.Password = value as string);
                            break;
                        case ComboBox comboBox:
                            comboBox.Dispatcher.Invoke(() => comboBox.SelectedItem = value);
                            break;
                        case CheckBox checkBox:
                            checkBox.Dispatcher.Invoke(() => checkBox.IsChecked = value is bool isChecked ? isChecked : bool.Parse(value as string));
                            break;
                    }
                }
            }
        }
    }

}
