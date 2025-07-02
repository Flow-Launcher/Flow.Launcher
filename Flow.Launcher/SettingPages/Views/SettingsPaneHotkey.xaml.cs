using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
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

            // Skip this plugin if all hotkeys are invisible
            var allHotkeyInvisible = hotkeyInfo.All(h => !h.Visible);
            if (allHotkeyInvisible) continue;

            var excard = new ExCard()
            {
                Title = metadata.Name,
                Margin = new Thickness(0, 4, 0, 0),
                // TODO: Support displaying plugin icon here
            };
            var hotkeyStackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            var sortedHotkeyInfo = hotkeyInfo.OrderBy(h => h.Id).ToList();
            foreach (var hotkey in hotkeyInfo)
            {
                // Skip invisible hotkeys
                if (!hotkey.Visible) continue;

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
                    var hotkeyControl = new HotkeyControl
                    {
                        Type = hotkey.HotkeyType == HotkeyType.Global ?
                            HotkeyControl.HotkeyType.GlobalPluginHotkey : HotkeyControl.HotkeyType.WindowPluginHotkey,
                        DefaultHotkey = hotkey.DefaultHotkey,
                        ValidateKeyGesture = true
                    };
                    hotkeyControl.SetHotkey(hotkeySetting, true);
                    hotkeyControl.ChangeHotkey = new RelayCommand<HotkeyModel>((m) => ChangePluginHotkey(metadata, hotkey, m));
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
            var oldHotkey = PluginManager.ChangePluginHotkey(metadata, globalPluginHotkey, newHotkey);
            HotKeyMapper.RemoveHotkey(oldHotkey);
            HotKeyMapper.SetGlobalPluginHotkey(globalPluginHotkey, metadata, newHotkey);
        }
        else if (pluginHotkey is SearchWindowPluginHotkey windowPluginHotkey)
        {
            var (oldHotkeyModel, newHotkeyModel) = PluginManager.ChangePluginHotkey(metadata, windowPluginHotkey, newHotkey);
            var windowPluginHotkeys = PluginManager.GetWindowPluginHotkeys();
            HotKeyMapper.RemoveWindowHotkey(oldHotkeyModel);
            HotKeyMapper.RemoveWindowHotkey(newHotkeyModel);
            HotKeyMapper.SetWindowHotkey(oldHotkeyModel, windowPluginHotkeys[oldHotkeyModel]);
            HotKeyMapper.SetWindowHotkey(newHotkeyModel, windowPluginHotkeys[newHotkeyModel]);
        }
    }
}
