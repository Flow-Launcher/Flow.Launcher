using System.Windows;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using System.Windows.Input;
using System.ComponentModel;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.BrowserBookmark.Models;

namespace Flow.Launcher.Plugin.BrowserBookmark.Views;

public partial class SettingsControl : INotifyPropertyChanged
{
    public Settings Settings { get; }
    private readonly PluginInitContext _context;
    public SettingsControl(Settings settings)
    {
        // Settings 객체를 직접 받도록 수정
        Settings = settings;
        InitializeComponent();
        DataContext = this;
    }
    public CustomBrowser SelectedCustomBrowser { get; set; }

    public bool LoadChromeBookmark
    {
        get => Settings.LoadChromeBookmark;
        set
        {
            Settings.LoadChromeBookmark = value;
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    public bool LoadFirefoxBookmark
    {
        get => Settings.LoadFirefoxBookmark;
        set
        {
            Settings.LoadFirefoxBookmark = value;
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    public bool LoadEdgeBookmark
    {
        get => Settings.LoadEdgeBookmark;
        set
        {
            Settings.LoadEdgeBookmark = value;
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    public bool OpenInNewBrowserWindow
    {
        get => Settings.OpenInNewBrowserWindow;
        set
        {
            Settings.OpenInNewBrowserWindow = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenInNewBrowserWindow)));
        }
    }
    public event PropertyChangedEventHandler PropertyChanged;

    private void NewCustomBrowser(object sender, RoutedEventArgs e)
    {
        var newBrowser = new CustomBrowser();
        var window = new CustomBrowserSettingWindow(newBrowser);
        window.ShowDialog();
        if (newBrowser is not
            {
                Name: null,
                DataDirectoryPath: null
            })
        {
            Settings.CustomChromiumBrowsers.Add(newBrowser);
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    private void DeleteCustomBrowser(object sender, RoutedEventArgs e)
    {
        if (CustomBrowsers.SelectedItem is CustomBrowser selectedCustomBrowser)
        {
            Settings.CustomChromiumBrowsers.Remove(selectedCustomBrowser);
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }

    private void MouseDoubleClickOnSelectedCustomBrowser(object sender, MouseButtonEventArgs e)
    {
        EditSelectedCustomBrowser();
    }

    private void Others_Click(object sender, RoutedEventArgs e)
    {
        CustomBrowsersList.Visibility = CustomBrowsersList.Visibility switch
        {
            Visibility.Collapsed => Visibility.Visible,
            _ => Visibility.Collapsed
        };
    }

    private void EditCustomBrowser(object sender, RoutedEventArgs e)
    {
        EditSelectedCustomBrowser();
    }

    private void EditSelectedCustomBrowser()
    {
        if (SelectedCustomBrowser is null)
            return;

        var window = new CustomBrowserSettingWindow(SelectedCustomBrowser);
        var result = window.ShowDialog() ?? false;
        if (result)
        {
            _ = Task.Run(() => Main.ReloadAllBookmarks());
        }
    }
    private void RemoveFaviconCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 플러그인 디렉토리 경로 가져오기
            var pluginDir = Main.GetPluginDirectory();
            if (string.IsNullOrEmpty(pluginDir))
            {
                MessageBox.Show("플러그인 디렉토리를 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 파비콘 캐시 디렉토리 경로 지정
            string cacheDir = Path.Combine(pluginDir, "Images", "Favicons");
        
            // 디렉토리 존재 확인 및 생성
            if (!Directory.Exists(cacheDir))
            {
                MessageBox.Show("파비콘 캐시 디렉토리가 존재하지 않습니다. 캐시가 비어있거나 아직 생성되지 않았을 수 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 파일 수 확인
            var files = Directory.GetFiles(cacheDir);
            if (files.Length == 0)
            {
                MessageBox.Show("파비콘 캐시가 이미 비어 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 디렉토리 내 모든 파일 삭제
            int deletedCount = 0;
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Flow.Launcher.Infrastructure.Logger.Log.Exception(
                        $"Failed to delete favicon cache file: {file}", ex);
                }
            }

            // 북마크 다시 로드
            Main.ReloadAllBookmarks();

            // 완료 메시지 표시
            MessageBox.Show($"{deletedCount}개의 파비콘 캐시 파일이 삭제되었습니다.", "캐시 삭제 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Flow.Launcher.Infrastructure.Logger.Log.Exception("Failed to remove favicon cache", ex);
            MessageBox.Show($"파비콘 캐시 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
