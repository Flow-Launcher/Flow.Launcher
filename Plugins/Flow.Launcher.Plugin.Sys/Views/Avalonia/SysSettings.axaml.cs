using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;

namespace Flow.Launcher.Plugin.Sys.Views.Avalonia;

internal partial class SysSettings : UserControl
{
    private readonly Settings _settings;

    public SysSettings()
    {
        InitializeComponent();
        _settings = new Settings();
    }

    public SysSettings(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _settings = viewModel.Settings;
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnEditCommandKeywordClick(object? sender, RoutedEventArgs e)
    {
        if (_settings.SelectedCommand is { } command)
        {
            await EditCommandKeywordAsync(command);
        }
    }

    private async void OnCommandDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is Command && _settings.SelectedCommand is { } command)
        {
            await EditCommandKeywordAsync(command);
        }
    }

    private async System.Threading.Tasks.Task EditCommandKeywordAsync(Command command)
    {
        var keywordBox = new TextBox
        {
            Text = command.Keyword,
            AcceptsReturn = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new global::Avalonia.Thickness(10, 0)
        };

        var resetButton = new Button
        {
            Content = Localize.flowlauncher_plugin_sys_reset()
        };
        // Key is the default value of this command
        resetButton.Click += (_, _) => keywordBox.Text = command.Key;

        var warning = new TextBlock
        {
            Text = Localize.flowlauncher_plugin_sys_input_command_keyword(),
            Foreground = Brushes.Red,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        var keywordRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };
        var keywordLabel = new TextBlock
        {
            Text = Localize.flowlauncher_plugin_sys_command_keyword(),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(keywordLabel, 0);
        Grid.SetColumn(keywordBox, 1);
        Grid.SetColumn(resetButton, 2);
        keywordRow.Children.Add(keywordLabel);
        keywordRow.Children.Add(keywordBox);
        keywordRow.Children.Add(resetButton);

        var dialog = new FAContentDialog
        {
            Title = Localize.flowlauncher_plugin_sys_custom_command_keyword(),
            Content = new StackPanel
            {
                Spacing = 16,
                MinWidth = 450,
                Children =
                {
                    new TextBlock
                    {
                        Text = Localize.flowlauncher_plugin_sys_custom_command_keyword_tip(command.Name),
                        TextWrapping = TextWrapping.Wrap
                    },
                    keywordRow,
                    warning
                }
            },
            PrimaryButtonText = Localize.flowlauncher_plugin_sys_confirm(),
            CloseButtonText = Localize.flowlauncher_plugin_sys_cancel(),
            DefaultButton = FAContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            var keyword = keywordBox.Text;
            if (string.IsNullOrEmpty(keyword))
            {
                warning.IsVisible = true;
                args.Cancel = true;
                return;
            }

            command.Keyword = keyword;
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await dialog.ShowAsync(owner);
        }
        else
        {
            await dialog.ShowAsync();
        }
    }
}
