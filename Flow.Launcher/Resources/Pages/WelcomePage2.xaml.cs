using System.Windows.Media;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage2
    {
        public Settings Settings { get; private set; }
        private WelcomeViewModel _viewModel;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!IsInitialized)
            {
                Settings = Ioc.Default.GetRequiredService<Settings>();
                _viewModel = Ioc.Default.GetRequiredService<WelcomeViewModel>();
                InitializeComponent();
            }
            // Sometimes the navigation is not triggered by button click,
            // so we need to reset the page number
            _viewModel.PageNum = 2;
            base.OnNavigatedTo(e);
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
