using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Control = System.Windows.Controls.Control;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace Flow.Launcher.Core.Plugin
{
    public class JsonRPCPluginSettings
    {
        public required JsonRpcConfigurationModel? Configuration { get; init; }

        public required string SettingPath { get; init; }
        public Dictionary<string, FrameworkElement> SettingControls { get; } = new();

        public IReadOnlyDictionary<string, object> Inner => Settings;
        protected ConcurrentDictionary<string, object> Settings { get; set; }
        public required IPublicAPI API { get; init; }

        private JsonStorage<ConcurrentDictionary<string, object>> _storage;

        // maybe move to resource?
        private static readonly Thickness settingControlMargin = new(0, 9, 18, 9);
        private static readonly Thickness settingCheckboxMargin = new(0, 9, 9, 9);
        private static readonly Thickness settingPanelMargin = (Thickness)System.Windows.Application.Current.FindResource("SettingPanelMargin");

        public async Task InitializeAsync()
        {
            _storage = new JsonStorage<ConcurrentDictionary<string, object>>(SettingPath);
            Settings = await _storage.LoadAsync();

            if (Configuration == null)
            {
                return;
            }

            foreach (var (_, attributes) in Configuration.Body)
            {
                // Skip if the setting does not have attributes or name
                if (attributes?.Name == null)
                {
                    continue;
                }

                if (!Settings.ContainsKey(attributes.Name))
                {
                    Settings[attributes.Name] = attributes.DefaultValue;
                }
            }
        }

        public void UpdateSettings(IReadOnlyDictionary<string, object> settings)
        {
            if (settings == null || settings.Count == 0)
                return;

            foreach (var (key, value) in settings)
            {
                Settings[key] = value;

                if (SettingControls.TryGetValue(key, out var control))
                {
                    switch (control)
                    {
                        case TextBox textBox:
                            textBox.Dispatcher.Invoke(() => textBox.Text = value as string ?? string.Empty);
                            break;
                        case PasswordBox passwordBox:
                            passwordBox.Dispatcher.Invoke(() => passwordBox.Password = value as string ?? string.Empty);
                            break;
                        case ComboBox comboBox:
                            comboBox.Dispatcher.Invoke(() => comboBox.SelectedItem = value);
                            break;
                        case CheckBox checkBox:
                            checkBox.Dispatcher.Invoke(() =>
                                checkBox.IsChecked = value is bool isChecked
                                    ? isChecked
                                    : bool.Parse(value as string ?? string.Empty));
                            break;
                    }
                }
            }

            Save();
        }

        public async Task SaveAsync()
        {
            await _storage.SaveAsync();
        }

        public void Save()
        {
            _storage.Save();
        }

        public Control CreateSettingPanel()
        {
            // If there are no settings or the settings are empty, return null
            if (Settings == null || Settings.IsEmpty)
            {
                return null;
            }

            // Create main grid
            var mainPanel = new Grid { Margin = settingPanelMargin, VerticalAlignment = VerticalAlignment.Center };
            mainPanel.ColumnDefinitions.Add(new ColumnDefinition()
            {
                Width = new GridLength(70, GridUnitType.Star)  // TODO: Auto
            });
            mainPanel.ColumnDefinitions.Add(new ColumnDefinition()
            {
                Width = new GridLength(30, GridUnitType.Star)  // TODO: Auto
            });

            // Iterate over each setting and create one row for it
            int rowCount = 0;
            foreach (var (type, attributes) in Configuration.Body)
            {
                // Skip if the setting does not have attributes or name
                if (attributes?.Name == null)
                {
                    continue;
                }

                // Add a new row to the main grid
                mainPanel.RowDefinitions.Add(new RowDefinition());

                // State controls for column 0 and 1
                StackPanel panel = null;
                FrameworkElement contentControl;

                // If the type is textBlock or seperator, we do not need to create a panel
                if (type != "textBlock" && type != "seperator")
                {
                    // Create a panel to hold the label and description
                    panel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Create a text block for name
                    var name = new TextBlock()
                    {
                        Text = attributes.Label,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.WrapWithOverflow
                    };

                    // Create a text block for description
                    TextBlock desc = null;
                    if (attributes.Description != null)
                    {
                        desc = new TextBlock()
                        {
                            Text = attributes.Description,
                            FontSize = 12,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new(0, 2, 0, 0),  // TODO: Use resource
                            TextWrapping = TextWrapping.WrapWithOverflow
                        };

                        desc.SetResourceReference(TextBlock.ForegroundProperty, "Color04B"); // for theme change
                    }

                    // Add the name and description to the panel
                    panel.Children.Add(name);
                    if (desc != null)
                    {
                        panel.Children.Add(desc);
                    }
                }

                switch (type)
                {
                    case "textBlock":
                        {
                            contentControl = new TextBlock
                            {
                                Text = attributes.Description.Replace("\\r\\n", "\r\n"),
                                Padding = new Thickness(0, 0, 0, 0),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                                TextAlignment = TextAlignment.Left,
                                TextWrapping = TextWrapping.Wrap
                            };

                            break;
                        }
                    case "input":
                        {
                            var textBox = new TextBox()
                            {
                                Text = Settings[attributes.Name] as string ?? string.Empty,
                                Margin = settingControlMargin,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                                ToolTip = attributes.Description
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
                                Margin = new Thickness(10, 0, 0, 0),
                                Text = Settings[attributes.Name] as string ?? string.Empty,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                                ToolTip = attributes.Description
                            };

                            textBox.TextChanged += (_, _) =>
                            {
                                Settings[attributes.Name] = textBox.Text;
                            };

                            var Btn = new System.Windows.Controls.Button()
                            {
                                Margin = new Thickness(10, 0, 0, 0), Content = "Browse"
                            };

                            Btn.Click += (_, _) =>
                            {
                                using CommonDialog dialog = type switch
                                {
                                    "inputWithFolderBtn" => new FolderBrowserDialog(),
                                    _ => new OpenFileDialog(),
                                };
                                if (dialog.ShowDialog() != DialogResult.OK) return;

                                var path = dialog switch
                                {
                                    FolderBrowserDialog folderDialog => folderDialog.SelectedPath,
                                    OpenFileDialog fileDialog => fileDialog.FileName,
                                };
                                textBox.Text = path;
                                Settings[attributes.Name] = path;
                            };

                            var dockPanel = new DockPanel() { Margin = settingControlMargin };

                            DockPanel.SetDock(Btn, Dock.Right);
                            dockPanel.Children.Add(Btn);
                            dockPanel.Children.Add(textBox);
                            contentControl = dockPanel;

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
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
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
                                Margin = settingControlMargin,
                                Password = Settings[attributes.Name] as string ?? string.Empty,
                                PasswordChar = attributes.passwordChar == default ? '*' : attributes.passwordChar,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                                ToolTip = attributes.Description
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
                                Margin = settingControlMargin,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
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
                        var checkBox = new CheckBox
                        {
                            IsChecked =
                                Settings[attributes.Name] is bool isChecked
                                    ? isChecked
                                    : bool.Parse(attributes.DefaultValue),
                            Margin = settingCheckboxMargin,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                            ToolTip = attributes.Description
                        };

                        checkBox.Click += (sender, _) =>
                        {
                            Settings[attributes.Name] = ((CheckBox)sender).IsChecked;
                        };

                        contentControl = checkBox;

                        break;
                    case "hyperlink":
                        var hyperlink = new Hyperlink { ToolTip = attributes.Description, NavigateUri = attributes.url };

                        var linkbtn = new System.Windows.Controls.Button
                        {
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                            Margin = settingControlMargin
                        };

                        linkbtn.Content = attributes.urlLabel;

                        contentControl = linkbtn;

                        break;
                    case "seperator":  // TODO: Support seperator
                        // TODO: Use style for Seperator
                        contentControl = new Separator
                        {
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new(-70, 13.5, -18, 13.5),
                            Height = 1
                        };
                        contentControl.SetResourceReference(Separator.BackgroundProperty, "Color03B");

                        break;
                    default:
                        continue;
                }

                // If type is textBlock or seperator, we just add the content control to the main grid
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

                    // Add into SettingControls for later use if need
                    SettingControls[attributes.Name] = contentControl;
                }

                rowCount++;
            }

            // Wrap the main grid in a user control
            return new UserControl()
            {
                Content = mainPanel
            };
        }
    }
}
