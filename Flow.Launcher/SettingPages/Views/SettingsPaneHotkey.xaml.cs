using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Plugin;
using Flow.Launcher.Resources.Controls;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Controls;
using System.Threading.Tasks;
using System.Windows.Media;

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
                Header = metadata.Name + " " + Localize.hotkeys(),
                Margin = new Thickness(0, 4, 0, 0),
                HeaderIcon = new Image() { Source = ImageLoader.LoadingImage },
                Tag = metadata
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
                excard.Items.Add(card);
            }
            PluginHotkeySettings.Children.Add(excard);
        }

        // Load plugin icons into SettingsExpander asynchronously
        _ = LoadPluginIconsAsync();
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

    private async Task LoadPluginIconsAsync()
    {
        // Snapshot list to avoid collection modification issues
        var expanders = PluginHotkeySettings.Children
            .OfType<SettingsExpander>()
            .Where(e => e.Tag is PluginMetadata m && !string.IsNullOrEmpty(m.IcoPath))
            .ToList();

        // Fire all loads concurrently
        var tasks = expanders.Select(async expander =>
        {
            if (expander.Tag is not PluginMetadata metadata) return;
            try
            {
                var iconSource = await App.API.LoadImageAsync(metadata.IcoPath);
                if (iconSource == null) return;

                // Marshal back to UI thread if needed
                if (!Dispatcher.CheckAccess())
                {
                    await Dispatcher.InvokeAsync(() => ApplyIcon(expander, iconSource));
                }
                else
                {
                    ApplyIcon(expander, iconSource);
                }
            }
            catch
            {
                // Swallow exceptions to avoid impacting UI; optionally log if logging infra exists
            }
        });

        await Task.WhenAll(tasks);
    }

    private static void ApplyIcon(SettingsExpander expander, ImageSource iconSource)
    {
        if (expander.HeaderIcon is Image img)
        {
            img.Source = iconSource;
        }
        else
        {
            expander.HeaderIcon = new Image { Source = iconSource };
        }
    }
}
