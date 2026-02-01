using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;

#nullable enable

namespace Flow.Launcher.Core.Plugin
{
    public class JsonRPCPluginSettings : ISavable
    {
        public required JsonRpcConfigurationModel? Configuration { get; init; }

        public required string SettingPath { get; init; }
        public Dictionary<string, FrameworkElement> SettingControls { get; } = new();

        public IReadOnlyDictionary<string, object?> Inner => Settings;
        protected ConcurrentDictionary<string, object?> Settings { get; set; } = null!;
        public required IPublicAPI API { get; init; }

        private static readonly string ClassName = nameof(JsonRPCPluginSettings);

        private JsonStorage<ConcurrentDictionary<string, object?>> _storage = null!;

        private static readonly double MainGridColumn0MaxWidthRatio = 0.6;
        private static readonly Thickness SettingPanelMargin = (Thickness)Application.Current.FindResource("SettingPanelMargin");
        private static readonly Thickness SettingPanelItemLeftMargin = (Thickness)Application.Current.FindResource("SettingPanelItemLeftMargin");
        private static readonly Thickness SettingPanelItemTopBottomMargin = (Thickness)Application.Current.FindResource("SettingPanelItemTopBottomMargin");
        private static readonly Thickness SettingPanelItemLeftTopBottomMargin = (Thickness)Application.Current.FindResource("SettingPanelItemLeftTopBottomMargin");
        private static readonly double SettingPanelTextBoxMinWidth = (double)Application.Current.FindResource("SettingPanelTextBoxMinWidth");
        private static readonly double SettingPanelPathTextBoxWidth = (double)Application.Current.FindResource("SettingPanelPathTextBoxWidth");
        private static readonly double SettingPanelAreaTextBoxMinHeight = (double)Application.Current.FindResource("SettingPanelAreaTextBoxMinHeight");

        public async Task InitializeAsync()
        {
            if (Settings == null)
            {
                _storage = new JsonStorage<ConcurrentDictionary<string, object?>>(SettingPath);
                Settings = await _storage.LoadAsync();

                // Because value type of settings dictionary is object which causes them to be JsonElement when loading from json files,
                // we need to convert it to the correct type
                foreach (var (key, value) in Settings)
                {
                    if (value is not JsonElement jsonElement) continue;

                    Settings[key] = jsonElement.ValueKind switch
                    {
                        JsonValueKind.String => jsonElement.GetString() ?? value,
                        JsonValueKind.True => jsonElement.GetBoolean(),
                        JsonValueKind.False => jsonElement.GetBoolean(),
                        JsonValueKind.Null => null,
                        _ => value
                    };
                }
            }

            if (Configuration == null) return;

            foreach (var (type, attributes) in Configuration.Body)
            {
                // Skip if the setting does not have attributes or name
                if (attributes?.Name == null) continue;

                // Skip if the setting does not have attributes or name
                if (!NeedSaveInSettings(type)) continue;

                // If need save in settings, we need to make sure the setting exists in the settings file
                if (Settings.ContainsKey(attributes.Name)) continue;

                if (type == "checkbox")
                {
                    // If can parse the default value to bool, use it, otherwise use false
                    Settings[attributes.Name] = bool.TryParse(attributes.DefaultValue, out var value) && value;
                }
                else
                {
                    Settings[attributes.Name] = attributes.DefaultValue;
                }
            }
        }

        public void UpdateSettings(IReadOnlyDictionary<string, object> settings)
        {
            if (settings == null || settings.Count == 0) return;

            foreach (var (key, value) in settings)
            {
                Settings[key] = value;

                if (SettingControls.TryGetValue(key, out var control))
                {
                    switch (control)
                    {
                        case TextBox textBox:
                            var text = value as string ?? string.Empty;
                            textBox.Dispatcher.Invoke(() => textBox.Text = text);
                            break;
                        case PasswordBox passwordBox:
                            var password = value as string ?? string.Empty;
                            passwordBox.Dispatcher.Invoke(() => passwordBox.Password = password);
                            break;
                        case ComboBox comboBox:
                            comboBox.Dispatcher.Invoke(() => comboBox.SelectedItem = value);
                            break;
                        case CheckBox checkBox:
                            var isChecked = value is bool boolValue
                                ? boolValue
                                // If can parse the default value to bool, use it, otherwise use false
                                : value is string stringValue && bool.TryParse(stringValue, out var boolValueFromString)
                                    && boolValueFromString;
                            checkBox.Dispatcher.Invoke(() => checkBox.IsChecked = isChecked);
                            break;
                    }
                }
            }

            Save();
        }

        public async Task SaveAsync()
        {
            try
            {
                await _storage.SaveAsync();
            }
            catch (System.Exception e)
            {
                API.LogException(ClassName, $"Failed to save plugin settings to path: {SettingPath}", e);
            }
        }

        public void Save()
        {
            try
            {
                _storage.Save();
            }
            catch (System.Exception e)
            {
                API.LogException(ClassName, $"Failed to save plugin settings to path: {SettingPath}", e);
            }
        }
        
        public bool NeedCreateSettingPanel()
        {
            // If there are no settings or the settings configuration is empty, return null
            return Settings != null && Configuration != null && Configuration.Body.Count != 0;
        }

        public Control CreateSettingPanel()
        {
            if (!NeedCreateSettingPanel()) return null!;

            // Create main grid with two columns (Column 0: Auto, Column 1: *)
            var mainPanel = new Grid { Margin = SettingPanelMargin, VerticalAlignment = VerticalAlignment.Center };
            mainPanel.ColumnDefinitions.Add(new ColumnDefinition()
            {
                Width = new GridLength(0, GridUnitType.Auto)
            });
            mainPanel.ColumnDefinitions.Add(new ColumnDefinition()
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            // Iterate over each setting and create one row for it
            var rowCount = 0;
            foreach (var (type, attributes) in Configuration!.Body)
            {
                // Skip if the setting does not have attributes or name
                if (attributes?.Name == null) continue;

                // Add a new row to the main grid
                mainPanel.RowDefinitions.Add(new RowDefinition()
                {
                    Height = new GridLength(0, GridUnitType.Auto)
                });

                // State controls for column 0 and 1
                StackPanel? panel = null;
                FrameworkElement contentControl;

                // If the type is textBlock, separator, or checkbox, we do not need to create a panel
                if (type != "textBlock" && type != "separator" && type != "checkbox")
                {
                    // Create a panel to hold the label and description
                    panel = new StackPanel
                    {
                        Margin = SettingPanelItemTopBottomMargin,
                        Orientation = Orientation.Vertical,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Create a text block for name
                    var name = new TextBlock()
                    {
                        Text = attributes.Label,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };

                    // Create a text block for description
                    TextBlock? desc = null;
                    if (attributes.Description != null)
                    {
                        desc = new TextBlock()
                        {
                            Text = attributes.Description,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        };

                        desc.SetResourceReference(TextBlock.StyleProperty, "SettingPanelTextBlockDescriptionStyle"); // for theme change
                    }

                    // Add the name and description to the panel
                    panel.Children.Add(name);
                    if (desc != null) panel.Children.Add(desc);
                }

                switch (type)
                {
                    case "textBlock":
                        {
                            contentControl = new TextBlock
                            {
                                Text = attributes.Description?.Replace("\\r\\n", "\r\n") ?? string.Empty,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemTopBottomMargin,
                                TextAlignment = TextAlignment.Left,
                                TextWrapping = TextWrapping.Wrap
                            };

                            break;
                        }
                    case "input":
                        {
                            var textBox = new TextBox()
                            {
                                MinWidth = SettingPanelTextBoxMinWidth,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemLeftTopBottomMargin,
                                Text = Settings[attributes.Name] as string ?? string.Empty,
                                ToolTip = attributes.Description,
                                TextWrapping = TextWrapping.Wrap
                            };

                            textBox.TextChanged += (_, _) =>
                            {
                                Settings[attributes.Name] = textBox.Text;
                            };

                            contentControl = textBox;

                            break;
                        }
                    case "inputWithFileBtn":
                    case "inputWithFolderBtn":
                        {
                            var textBox = new TextBox()
                            {
                                Width = SettingPanelPathTextBoxWidth,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemLeftMargin,
                                Text = Settings[attributes.Name] as string ?? string.Empty,
                                ToolTip = attributes.Description,
                                TextWrapping = TextWrapping.Wrap
                            };

                            textBox.TextChanged += (_, _) =>
                            {
                                Settings[attributes.Name] = textBox.Text;
                            };

                            var Btn = new Button()
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemLeftMargin,
                                Content = Localize.select()
                            };

                            Btn.Click += (_, _) =>
                            {
                                using System.Windows.Forms.CommonDialog dialog = type switch
                                {
                                    "inputWithFolderBtn" => new System.Windows.Forms.FolderBrowserDialog(),
                                    _ => new System.Windows.Forms.OpenFileDialog(),
                                };

                                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                                {
                                    return;
                                }

                                var path = dialog switch
                                {
                                    System.Windows.Forms.FolderBrowserDialog folderDialog => folderDialog.SelectedPath,
                                    System.Windows.Forms.OpenFileDialog fileDialog => fileDialog.FileName,
                                    _ => throw new System.NotImplementedException()
                                };

                                textBox.Text = path;
                                Settings[attributes.Name] = path;
                            };

                            var stackPanel = new StackPanel()
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemTopBottomMargin,
                                Orientation = Orientation.Horizontal
                            };

                            // Create a stack panel to wrap the button and text box
                            stackPanel.Children.Add(textBox);
                            stackPanel.Children.Add(Btn);

                            contentControl = stackPanel;

                            break;
                        }
                    case "textarea":
                        {
                            var textBox = new TextBox()
                            {
                                MinHeight = SettingPanelAreaTextBoxMinHeight,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemLeftTopBottomMargin,
                                TextWrapping = TextWrapping.Wrap,
                                AcceptsReturn = true,
                                Text = Settings[attributes.Name] as string ?? string.Empty,
                                ToolTip = attributes.Description
                            };

                            textBox.TextChanged += (sender, _) =>
                            {
                                Settings[attributes.Name] = ((TextBox)sender).Text;
                            };

                            contentControl = textBox;

                            break;
                        }
                    case "passwordBox":
                        {
                            var passwordBox = new PasswordBox()
                            {
                                MinWidth = SettingPanelTextBoxMinWidth,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemLeftTopBottomMargin,
                                Password = Settings[attributes.Name] as string ?? string.Empty,
                                PasswordChar = attributes.passwordChar == default ? '*' : attributes.passwordChar,
                                ToolTip = attributes.Description,
                            };

                            passwordBox.PasswordChanged += (sender, _) =>
                            {
                                Settings[attributes.Name] = ((PasswordBox)sender).Password;
                            };

                            contentControl = passwordBox;

                            break;
                        }
                    case "dropdown":
                        {
                            var comboBox = new ComboBox()
                            {
                                ItemsSource = attributes.Options,
                                SelectedItem = Settings[attributes.Name],
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemLeftTopBottomMargin,
                                ToolTip = attributes.Description
                            };

                            comboBox.SelectionChanged += (sender, _) =>
                            {
                                Settings[attributes.Name] = (string)((ComboBox)sender).SelectedItem;
                            };

                            contentControl = comboBox;

                            break;
                        }
                    case "checkbox":
                        {
                            // If can parse the default value to bool, use it, otherwise use false
                            var defaultValue = bool.TryParse(attributes.DefaultValue, out var value) && value;
                            var checkBox = new CheckBox
                            {
                                IsChecked =
                                Settings[attributes.Name] is bool isChecked
                                    ? isChecked
                                    : defaultValue,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemTopBottomMargin,
                                Content = attributes.Label,
                                ToolTip = attributes.Description
                            };

                            checkBox.Click += (sender, _) =>
                            {
                                Settings[attributes.Name] = ((CheckBox)sender).IsChecked ?? defaultValue;
                            };

                            contentControl = checkBox;

                            break;
                        }
                    case "hyperlink":
                        {
                            var hyperlink = new Hyperlink
                            {
                                ToolTip = attributes.Description,
                                NavigateUri = attributes.url
                            };

                            hyperlink.Inlines.Add(attributes.urlLabel);
                            hyperlink.RequestNavigate += (sender, e) =>
                            {
                                API.OpenUrl(e.Uri);
                                e.Handled = true;
                            };

                            var textBlock = new TextBlock()
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = SettingPanelItemLeftTopBottomMargin,
                                TextAlignment = TextAlignment.Left,
                                TextWrapping = TextWrapping.Wrap
                            };
                            textBlock.Inlines.Add(hyperlink);

                            contentControl = textBlock;

                            break;
                        }
                    case "separator":
                        {
                            var sep = new Separator();

                            sep.SetResourceReference(Separator.StyleProperty, "SettingPanelSeparatorStyle");

                            contentControl = sep;

                            break;
                        }
                    default:
                        continue;
                }

                // If type is textBlock or separator, we just add the content control to the main grid
                if (panel == null)
                {
                    // Add the content control to the column 0, row rowCount and columnSpan 2 of the main grid
                    mainPanel.Children.Add(contentControl);
                    Grid.SetColumn(contentControl, 0);
                    Grid.SetColumnSpan(contentControl, 2);
                    Grid.SetRow(contentControl, rowCount);
                }
                else
                {
                    // Add the panel to the column 0 and row rowCount of the main grid
                    mainPanel.Children.Add(panel);
                    Grid.SetColumn(panel, 0);
                    Grid.SetRow(panel, rowCount);

                    // Add the content control to the column 1 and row rowCount of the main grid
                    mainPanel.Children.Add(contentControl);
                    Grid.SetColumn(contentControl, 1);
                    Grid.SetRow(contentControl, rowCount);
                }

                // Add into SettingControls for settings storage if need
                if (NeedSaveInSettings(type)) SettingControls[attributes.Name] = contentControl;

                rowCount++;
            }

            mainPanel.SizeChanged += MainPanel_SizeChanged;

            // Wrap the main grid in a user control
            return new UserControl()
            {
                Content = mainPanel
            };
        }

        private void MainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not Grid grid) return;

            var workingWidth = grid.ActualWidth;

            if (workingWidth <= 0) return;

            var constrainedWidth = MainGridColumn0MaxWidthRatio * workingWidth;

            // Set MaxWidth of column 0 and its children
            // We must set MaxWidth of its children to make text wrapping work correctly
            grid.ColumnDefinitions[0].MaxWidth = constrainedWidth;
            foreach (var child in grid.Children)
            {
                if (child is FrameworkElement element && Grid.GetColumn(element) == 0 && Grid.GetColumnSpan(element) == 1)
                {
                    element.MaxWidth = constrainedWidth;
                }
            }
        }

        private static bool NeedSaveInSettings(string type)
        {
            return type != "textBlock" && type != "separator" && type != "hyperlink";
        }
    }
}
