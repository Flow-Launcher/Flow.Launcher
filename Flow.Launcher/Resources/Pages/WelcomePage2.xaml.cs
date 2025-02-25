using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.ViewModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage2
    {
        public Settings Settings { get; set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.ExtraData is Settings settings)
                Settings = settings;
            else
                throw new ArgumentException("Unexpected Parameter setting.");

            InitializeComponent();
        }

        [RelayCommand]
        private static void SetTogglingHotkey(HotkeyModel hotkey)
        {
            HotKeyMapper.SetHotkey(hotkey, HotKeyMapper.OnToggleHotkey);
        }

        public Brush PreviewBackground
        {
            get
            {
                var wallpaper = WallpaperPathRetrieval.GetWallpaperPath();
                if (wallpaper is not null && File.Exists(wallpaper))
                {
                    var memStream = new MemoryStream(File.ReadAllBytes(wallpaper));
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = memStream;
                    bitmap.DecodePixelWidth = 800;
                    bitmap.DecodePixelHeight = 600;
                    bitmap.EndInit();
                    return new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                }

                var wallpaperColor = WallpaperPathRetrieval.GetWallpaperColor();
                return new SolidColorBrush(wallpaperColor);
            }
        }
    }
}
