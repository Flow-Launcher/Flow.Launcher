using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public class JsonRPCPluginSettings
    {
        public required JsonRpcConfigurationModel Configuration { get; init; }

        public required string SettingPath { get; init; }
        public Dictionary<string, FrameworkElement> SettingControls { get; } = new();
        
        public IReadOnlyDictionary<string, object> Inner => Settings;
        protected Dictionary<string, object> Settings { get; set; }
        public required IPublicAPI API { get; init; }
        
        private JsonStorage<Dictionary<string, object>> _storage;

        // maybe move to resource?
        private static readonly Thickness settingControlMargin = new(0, 9, 18, 9);
        private static readonly Thickness settingCheckboxMargin = new(0, 9, 9, 9);
        private static readonly Thickness settingPanelMargin = new(0, 0, 0, 0);
        private static readonly Thickness settingTextBlockMargin = new(70, 9, 18, 9);
        private static readonly Thickness settingLabelPanelMargin = new(70, 9, 18, 9);
        private static readonly Thickness settingLabelMargin = new(0, 0, 0, 0);
        private static readonly Thickness settingDescMargin = new(0, 2, 0, 0);
        private static readonly Thickness settingSepMargin = new(0, 0, 0, 2);

        public async Task InitializeAsync()
        {
            _storage = new JsonStorage<Dictionary<string, object>>(SettingPath);
            Settings = await _storage.LoadAsync();

            foreach (var (type, attributes) in Configuration.Body) 
            {
                if (attributes.Name == null)
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
                if (Settings.ContainsKey(key))
                {
                    Settings[key] = value;
                }

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
                            checkBox.Dispatcher.Invoke(() => checkBox.IsChecked = value is bool isChecked ? isChecked : bool.Parse(value as string ?? string.Empty));
                            break;
                    }
                }
            }
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

            foreach (var (type, attribute) in Configuration.Body)
            {
                Separator sep = new Separator();
                sep.VerticalAlignment = VerticalAlignment.Top;
                sep.Margin = settingSepMargin;
                sep.SetResourceReference(Separator.BackgroundProperty, "Color03B"); /* for theme change */
                var panel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
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
                    Text = attribute.Description,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = settingDescMargin,
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
                            Padding = new Thickness(0, 0, 0, 0),
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
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
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
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
                    case "inputWithFileBtn":
                    {
                        var textBox = new TextBox()
                        {
                            Margin = new Thickness(10, 0, 0, 0),
                            Text = Settings[attribute.Name] as string ?? string.Empty,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                            ToolTip = attribute.Description
                        };

                        textBox.TextChanged += (_, _) =>
                        {
                            Settings[attribute.Name] = textBox.Text;
                        };

                        var Btn = new System.Windows.Controls.Button()
                        {
                            Margin = new Thickness(10, 0, 0, 0), Content = "Browse"
                        };

                        var dockPanel = new DockPanel()
                        {
                            Margin = settingControlMargin
                        };

                        DockPanel.SetDock(Btn, Dock.Right);
                        dockPanel.Children.Add(Btn);
                        dockPanel.Children.Add(textBox);
                        contentControl = dockPanel;
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
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
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
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
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
                        var comboBox = new System.Windows.Controls.ComboBox()
                        {
                            ItemsSource = attribute.Options,
                            SelectedItem = Settings[attribute.Name],
                            Margin = settingControlMargin,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                            ToolTip = attribute.Description
                        };

                        comboBox.SelectionChanged += (sender, _) =>
                        {
                            Settings[attribute.Name] = (string)((System.Windows.Controls.ComboBox)sender).SelectedItem;
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
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
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
                            ToolTip = attribute.Description, NavigateUri = attribute.url
                        };

                        var linkbtn = new System.Windows.Controls.Button
                        {
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = settingControlMargin
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
                    SettingControls[attribute.Name] = contentControl;

                mainPanel.Children.Add(panel);
                mainPanel.Children.Add(contentControl);
                rowCount++;

            }

            return settingWindow;
        }


    }
}
