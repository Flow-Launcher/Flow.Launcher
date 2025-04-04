using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Controls;
using System.Windows.Media;
using ModernWpf.Controls;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPanePlugins
{
    private SettingsPanePluginsViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();
            _viewModel = new SettingsPanePluginsViewModel(settings);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
    
    private void DisplayModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SettingsPanePluginsViewModel viewModel)
        {
            viewModel.UpdateDisplayModeFromSelection();
        }
    }

    private void SettingsPanePlugins_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.F || Keyboard.Modifiers is not ModifierKeys.Control) return;
        PluginFilterTextbox.Focus();
    }
    
    
    private async void Help_Click(object sender, RoutedEventArgs e)
    {
        var helpDialog = new ContentDialog()
        {
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["changePriorityWindow"],
                        FontSize = 18,
                        Margin = new Thickness(0, 0, 0, 10),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["priority_tips"],
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["searchDelayTimeTitle"],
                        FontSize = 18,
                        Margin = new Thickness(0, 24, 0, 10),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = (string)Application.Current.Resources["searchDelayTime_tips"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },
            
            PrimaryButtonText = (string)Application.Current.Resources["commonOK"],
            CornerRadius = new CornerRadius(8),
            Style = (Style)Application.Current.Resources["ContentDialog"]
        };
        
        await helpDialog.ShowAsync();
    }
}
