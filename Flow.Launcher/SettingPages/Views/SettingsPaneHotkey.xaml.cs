using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using Flow.Launcher.Resources.Controls;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Controls;

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

            // Skip this plugin if all hotkeys are invisible
            var allHotkeyInvisible = hotkeyInfo.All(h => !h.Visible);
            if (allHotkeyInvisible) continue;

            var excard = new SettingsExpander()
            {
                Header = metadata.Name,
                Margin = new Thickness(0, 4, 0, 0),
            };
            var hotkeyStackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            var sortedHotkeyInfo = hotkeyInfo.OrderBy(h => h.Id).ToList();
            foreach (var hotkey in sortedHotkeyInfo)
            {
                // Skip invisible hotkeys
                if (!hotkey.Visible) continue;

                var card = new SettingsCard()
                {
                    Header = hotkey.Name,
                    Description = hotkey.Description,
                    HeaderIcon = new FontIcon() { Glyph = hotkey.Glyph.Glyph }
                };
                var hotkeySetting = metadata.PluginHotkeys.Find(h => h.Id == hotkey.Id)?.Hotkey ?? hotkey.DefaultHotkey;
                if (hotkey.Editable)
                {
                    var hotkeyControl = new HotkeyControl
                    {
                        Type = hotkey.HotkeyType == HotkeyType.Global ?
                            HotkeyControl.HotkeyType.GlobalPluginHotkey :
                            HotkeyControl.HotkeyType.WindowPluginHotkey,
                        DefaultHotkey = hotkey.DefaultHotkey,
                        ValidateKeyGesture = false,
                        Hotkey = hotkeySetting,
                        ChangeHotkey = new RelayCommand<HotkeyModel>((h) => ChangePluginHotkey(metadata, hotkey, h))
                    };
                    card.Content = hotkeyControl;
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

    private static void ChangePluginHotkey(PluginMetadata metadata, BasePluginHotkey pluginHotkey, HotkeyModel newHotkey)
    {
        if (pluginHotkey is GlobalPluginHotkey globalPluginHotkey)
        {
            PluginManager.ChangePluginHotkey(metadata, globalPluginHotkey, newHotkey);
        }
        else if (pluginHotkey is SearchWindowPluginHotkey windowPluginHotkey)
        {
            PluginManager.ChangePluginHotkey(metadata, windowPluginHotkey, newHotkey);
        }
    }
}
