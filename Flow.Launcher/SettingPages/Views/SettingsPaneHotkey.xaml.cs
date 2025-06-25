using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Resources.Controls;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneHotkey
{
    private SettingsPaneHotkeyViewModel _viewModel = null!;
    private readonly SettingWindowViewModel _settingViewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        // Sometimes the navigation is not triggered by button click,
        // so we need to reset the page type
        _settingViewModel.PageType = typeof(SettingsPaneHotkey);

        // If the navigation is not triggered by button click, view model will be null again
        if (_viewModel == null)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPaneHotkeyViewModel>();
            DataContext = _viewModel;
        }
        if (!IsInitialized)
        {
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }

    private void PluginHotkeySettings_Loaded(object sender, RoutedEventArgs e)
    {
        var pluginHotkeyInfos = PluginManager.GetPluginHotkeyInfo();
        foreach (var info in pluginHotkeyInfos)
        {
            var pluginPair = info.Key;
            var hotkeyInfo = info.Value;
            var metadata = pluginPair.Metadata;
            var excard = new ExCard()
            {
                Title = metadata.Name
                // TODO: Support displaying plugin icon here
            };
            var hotkeyStackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            var sortedHotkeyInfo = hotkeyInfo.OrderBy(h => h.Id).ToList();
            foreach (var hotkey in hotkeyInfo)
            {
                var card = new Card()
                {
                    Title = hotkey.Name,
                    Sub = hotkey.Description,
                    Icon = hotkey.Glyph.Glyph,
                    Type = Card.CardType.Inside
                };
                var hotkeySetting = metadata.PluginHotkeys.Find(h => h.Id == hotkey.Id)?.Hotkey ?? hotkey.DefaultHotkey;
                if (hotkey.Editable)
                {
                    // TODO: Check if this can use
                    var hotkeyControl = new HotkeyControl
                    {
                        DefaultHotkey = hotkey.DefaultHotkey,
                        Hotkey = hotkeySetting,
                        Type = HotkeyControl.HotkeyType.CustomQueryHotkey,
                        ValidateKeyGesture = true
                    };
                    card.Content = hotkeyControl;
                    // TODO: Update metadata & plugin setting hotkey
                }
                else
                {
                    var hotkeyDisplay = new HotkeyDisplay
                    {
                        Keys = hotkeySetting
                    };
                    card.Content = hotkeyDisplay;
                }
                hotkeyStackPanel.Children.Add(card);
            }
            excard.Content = hotkeyStackPanel;
            PluginHotkeySettings.Children.Add(excard);
        }
    }
}
