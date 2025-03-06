using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.ViewModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage2
    {
        public Settings Settings { get; set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Settings = Ioc.Default.GetRequiredService<Settings>();
            // Sometimes the navigation is not triggered by button click,
            // so we need to reset the page number
            Ioc.Default.GetRequiredService<WelcomeViewModel>().PageNum = 2;
            InitializeComponent();
        }

        [RelayCommand]
        private static void SetTogglingHotkey(HotkeyModel hotkey)
        {
            HotKeyMapper.SetHotkey(hotkey, HotKeyMapper.OnToggleHotkey);
        }

        public Brush PreviewBackground
        {
            get => WallpaperPathRetrieval.GetWallpaperBrush();
        }
    }
}
