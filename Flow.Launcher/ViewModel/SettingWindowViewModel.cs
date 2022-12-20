using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace Flow.Launcher.ViewModel
{
    public partial class SettingWindowViewModel : BaseModel
    {
        private readonly Updater _updater;
        private readonly IPortable _portable;
        private readonly FlowLauncherJsonStorage<Settings> _storage;

        public SettingWindowViewModel(Updater updater, IPortable portable)
        {
            _updater = updater;
            _portable = portable;
            _storage = new FlowLauncherJsonStorage<Settings>();
            Settings = _storage.Load();
            Settings.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Settings.ActivateTimes):
                        OnPropertyChanged(nameof(ActivatedTimes));
                        break;
                    case nameof(Settings.WindowSize):
                        OnPropertyChanged(nameof(WindowWidthSize));
                        break;
                    case nameof(Settings.UseDate):
                    case nameof(Settings.DateFormat):
                        OnPropertyChanged(nameof(DateText));
                        break;
                    case nameof(Settings.UseClock):
                    case nameof(Settings.TimeFormat):
                        OnPropertyChanged(nameof(ClockText));
                        break;
                    case nameof(Settings.Language):
                        OnPropertyChanged(nameof(ClockText));
                        OnPropertyChanged(nameof(DateText));
                        break;
                }
            };

        }

        public Settings Settings { get; set; }

        public async void UpdateApp()
        {
            await _updater.UpdateAppAsync(App.API, false);
        }

        public bool AutoUpdates
        {
            get => Settings.AutoUpdates;
            set
            {
                Settings.AutoUpdates = value;

                if (value)
                {
                    UpdateApp();
                }
            }
        }

        public CultureInfo Culture => CultureInfo.DefaultThreadCurrentCulture;

        public bool StartFlowLauncherOnSystemStartup
        {
            get => Settings.StartFlowLauncherOnSystemStartup;
            set
            {
                Settings.StartFlowLauncherOnSystemStartup = value;

                try
                {
                    if (value)
                        AutoStartup.Enable();
                    else
                        AutoStartup.Disable();
                }
                catch (Exception e)
                {
                    Notification.Show(InternationalizationManager.Instance.GetTranslation("setAutoStartFailed"), e.Message);
                }
            }
        }

        // This is only required to set at startup. When portable mode enabled/disabled a restart is always required
        private bool _portableMode = DataLocation.PortableDataLocationInUse();
        public bool PortableMode
        {
            get => _portableMode;
            set
            {
                if (!_portable.CanUpdatePortability())
                    return;

                if (DataLocation.PortableDataLocationInUse())
                {
                    _portable.DisablePortableMode();
                }
                else
                {
                    _portable.EnablePortableMode();
                }
            }
        }

        public void Save()
        {
            foreach (var vm in PluginViewModels)
            {
                var id = vm.PluginPair.Metadata.ID;

                Settings.PluginSettings.Plugins[id].Disabled = vm.PluginPair.Metadata.Disabled;
                Settings.PluginSettings.Plugins[id].Priority = vm.Priority;
            }

            PluginManager.Save();
            _storage.Save();
        }

        public string GetFileFromDialog(string title, string filter = "")
        {
            var dlg = new System.Windows.Forms.OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Title = title,
                Filter = filter
            };

            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                return dlg.FileName;
            }
            else
            {
                return string.Empty;
            }
        }

        #region general

        // todo a better name?
        public class LastQueryMode : BaseModel
        {
            public string Display { get; set; }
            public Infrastructure.UserSettings.LastQueryMode Value { get; set; }
        }

        private List<LastQueryMode> _lastQueryModes = new List<LastQueryMode>();
        public List<LastQueryMode> LastQueryModes
        {
            get
            {
                if (_lastQueryModes.Count == 0)
                {
                    _lastQueryModes = InitLastQueryModes();
                }
                return _lastQueryModes;
            }
        }

        private List<LastQueryMode> InitLastQueryModes()
        {
            var modes = new List<LastQueryMode>();
            var enums = (Infrastructure.UserSettings.LastQueryMode[])Enum.GetValues(typeof(Infrastructure.UserSettings.LastQueryMode));
            foreach (var e in enums)
            {
                var key = $"LastQuery{e}";
                var display = _translater.GetTranslation(key);
                var m = new LastQueryMode
                {
                    Display = display, Value = e,
                };
                modes.Add(m);
            }
            return modes;
        }

        private void UpdateLastQueryModeDisplay()
        {
            foreach (var item in LastQueryModes)
            {
                item.Display = _translater.GetTranslation($"LastQuery{item.Value}");
            }
        }

        public string Language
        {
            get
            {
                return Settings.Language;
            }
            set
            {
                InternationalizationManager.Instance.ChangeLanguage(value);

                if (InternationalizationManager.Instance.PromptShouldUsePinyin(value))
                    ShouldUsePinyin = true;

                UpdateLastQueryModeDisplay();
            }
        }

        public bool ShouldUsePinyin
        {
            get
            {
                return Settings.ShouldUsePinyin;
            }
            set
            {
                Settings.ShouldUsePinyin = value;
            }
        }

        public List<string> QuerySearchPrecisionStrings
        {
            get
            {
                var precisionStrings = new List<string>();

                var enumList = Enum.GetValues(typeof(SearchPrecisionScore)).Cast<SearchPrecisionScore>().ToList();

                enumList.ForEach(x => precisionStrings.Add(x.ToString()));

                return precisionStrings;
            }
        }

        public List<string> OpenResultModifiersList => new List<string>
        {
            KeyConstant.Alt,
            KeyConstant.Ctrl,
            $"{KeyConstant.Ctrl}+{KeyConstant.Alt}"
        };
        private Internationalization _translater => InternationalizationManager.Instance;
        public List<Language> Languages => _translater.LoadAvailableLanguages();
        public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

        public ObservableCollection<CustomShortcutModel> CustomShortcuts => Settings.CustomShortcuts;
        public ObservableCollection<BuiltinShortcutModel> BuiltinShortcuts => Settings.BuiltinShortcuts;

        public string TestProxy()
        {
            var proxyServer = Settings.Proxy.Server;
            var proxyUserName = Settings.Proxy.UserName;
            if (string.IsNullOrEmpty(proxyServer))
            {
                return InternationalizationManager.Instance.GetTranslation("serverCantBeEmpty");
            }
            if (Settings.Proxy.Port <= 0)
            {
                return InternationalizationManager.Instance.GetTranslation("portCantBeEmpty");
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_updater.GitHubRepository);

            if (string.IsNullOrEmpty(proxyUserName) || string.IsNullOrEmpty(Settings.Proxy.Password))
            {
                request.Proxy = new WebProxy(proxyServer, Settings.Proxy.Port);
            }
            else
            {
                request.Proxy = new WebProxy(proxyServer, Settings.Proxy.Port)
                {
                    Credentials = new NetworkCredential(proxyUserName, Settings.Proxy.Password)
                };
            }
            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return InternationalizationManager.Instance.GetTranslation("proxyIsCorrect");
                }
                else
                {
                    return InternationalizationManager.Instance.GetTranslation("proxyConnectFailed");
                }
            }
            catch
            {
                return InternationalizationManager.Instance.GetTranslation("proxyConnectFailed");
            }
        }

        #endregion

        #region plugin

        public static string Plugin => @"https://github.com/Flow-Launcher/Flow.Launcher.PluginsManifest";
        public PluginViewModel SelectedPlugin { get; set; }

        public IList<PluginViewModel> PluginViewModels
        {
            get
            {
                var metadatas = PluginManager.AllPlugins
                    .OrderBy(x => x.Metadata.Disabled)
                    .ThenBy(y => y.Metadata.Name)
                    .Select(p => new PluginViewModel
                    {
                        PluginPair = p
                    })
                    .ToList();
                return metadatas;
            }
        }

        public IList<PluginStoreItemViewModel> ExternalPlugins
        {
            get
            {
                return LabelMaker(PluginsManifest.UserPlugins);
            }
        }

        private IList<PluginStoreItemViewModel> LabelMaker(IList<UserPlugin> list)
        {
            return list.Select(p => new PluginStoreItemViewModel(p))
                .OrderByDescending(p => p.Category == PluginStoreItemViewModel.NewRelease)
                .ThenByDescending(p => p.Category == PluginStoreItemViewModel.RecentlyUpdated)
                .ThenByDescending(p => p.Category == PluginStoreItemViewModel.None)
                .ThenByDescending(p => p.Category == PluginStoreItemViewModel.Installed)
                .ToList();
        }

        public Control SettingProvider
        {
            get
            {
                var settingProvider = SelectedPlugin.PluginPair.Plugin as ISettingProvider;
                if (settingProvider != null)
                {
                    var control = settingProvider.CreateSettingPanel();
                    control.HorizontalAlignment = HorizontalAlignment.Stretch;
                    control.VerticalAlignment = VerticalAlignment.Stretch;
                    return control;
                }
                else
                {
                    return new Control();
                }
            }
        }

        [RelayCommand]
        private async Task RefreshExternalPluginsAsync()
        {
            await PluginsManifest.UpdateManifestAsync();
            OnPropertyChanged(nameof(ExternalPlugins));
        }
        
        

        internal void DisplayPluginQuery(string queryToDisplay, PluginPair plugin, int actionKeywordPosition = 0)
        {
            var actionKeyword = plugin.Metadata.ActionKeywords.Count == 0
                ? string.Empty
                : plugin.Metadata.ActionKeywords[actionKeywordPosition];

            App.API.ChangeQuery($"{actionKeyword} {queryToDisplay}");
            App.API.ShowMainWindow();
        }

        #endregion

        #region theme

        public static string Theme => @"https://flowlauncher.com/docs/#/how-to-create-a-theme";
        public static string ThemeGallery => @"https://github.com/Flow-Launcher/Flow.Launcher/discussions/1438";

        public string SelectedTheme
        {
            get { return Settings.Theme; }
            set
            {
                Settings.Theme = value;
                ThemeManager.Instance.ChangeTheme(value);

                if (ThemeManager.Instance.BlurEnabled && Settings.UseDropShadowEffect)
                    DropShadowEffect = false;
            }
        }

        public List<string> Themes
            => ThemeManager.Instance.LoadAvailableThemes().Select(Path.GetFileNameWithoutExtension).ToList();

        public bool DropShadowEffect
        {
            get { return Settings.UseDropShadowEffect; }
            set
            {
                if (ThemeManager.Instance.BlurEnabled && value)
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("shadowEffectNotAllowed"));
                    return;
                }

                if (value)
                {
                    ThemeManager.Instance.AddDropShadowEffectToCurrentTheme();
                }
                else
                {
                    ThemeManager.Instance.RemoveDropShadowEffectFromCurrentTheme();
                }

                Settings.UseDropShadowEffect = value;
            }
        }

        public class ColorScheme
        {
            public string Display { get; set; }
            public ColorSchemes Value { get; set; }
        }

        public List<ColorScheme> ColorSchemes
        {
            get
            {
                List<ColorScheme> modes = new List<ColorScheme>();
                var enums = (ColorSchemes[])Enum.GetValues(typeof(ColorSchemes));
                foreach (var e in enums)
                {
                    var key = $"ColorScheme{e}";
                    var display = _translater.GetTranslation(key);
                    var m = new ColorScheme
                    {
                        Display = display, Value = e,
                    };
                    modes.Add(m);
                }
                return modes;
            }
        }

        public class SearchWindowPosition
        {
            public string Display { get; set; }
            public SearchWindowPositions Value { get; set; }
        }

        public List<SearchWindowPosition> SearchWindowPositions
        {
            get
            {
                List<SearchWindowPosition> modes = new List<SearchWindowPosition>();
                var enums = (SearchWindowPositions[])Enum.GetValues(typeof(SearchWindowPositions));
                foreach (var e in enums)
                {
                    var key = $"SearchWindowPosition{e}";
                    var display = _translater.GetTranslation(key);
                    var m = new SearchWindowPosition
                    {
                        Display = display, Value = e,
                    };
                    modes.Add(m);
                }
                return modes;
            }
        }

        public List<string> TimeFormatList { get; } = new()
        {
            "h:mm",
            "hh:mm",
            "H:mm",
            "HH:mm",
            "tt h:mm",
            "tt hh:mm",
            "h:mm tt",
            "hh:mm tt"
        };

        public List<string> DateFormatList { get; } = new()
        {
            "MM'/'dd dddd",
            "MM'/'dd ddd",
            "MM'/'dd",
            "MM'-'dd",
            "MMMM', 'dd",
            "dd'/'MM",
            "dd'-'MM",
            "ddd MM'/'dd",
            "dddd MM'/'dd",
            "dddd",
            "ddd dd'/'MM",
            "dddd dd'/'MM",
            "dddd dd', 'MMMM",
            "dd', 'MMMM"
        };

        public string TimeFormat
        {
            get => Settings.TimeFormat;
            set => Settings.TimeFormat = value;
        }

        public string DateFormat
        {
            get => Settings.DateFormat;
            set => Settings.DateFormat = value;
        }

        public string ClockText => DateTime.Now.ToString(TimeFormat, Culture);

        public string DateText => DateTime.Now.ToString(DateFormat, Culture);

        public double WindowWidthSize
        {
            get => Settings.WindowSize;
            set => Settings.WindowSize = value;
        }

        public bool UseGlyphIcons
        {
            get => Settings.UseGlyphIcons;
            set => Settings.UseGlyphIcons = value;
        }

        public bool UseAnimation
        {
            get => Settings.UseAnimation;
            set => Settings.UseAnimation = value;
        }

        public bool UseSound
        {
            get => Settings.UseSound;
            set => Settings.UseSound = value;
        }

        public bool UseClock
        {
            get => Settings.UseClock;
            set => Settings.UseClock = value;
        }

        public bool UseDate
        {
            get => Settings.UseDate;
            set => Settings.UseDate = value;
        }

        public double SettingWindowWidth
        {
            get => Settings.SettingWindowWidth;
            set => Settings.SettingWindowWidth = value;
        }

        public double SettingWindowHeight
        {
            get => Settings.SettingWindowHeight;
            set => Settings.SettingWindowHeight = value;
        }

        public double SettingWindowTop
        {
            get => Settings.SettingWindowTop;
            set => Settings.SettingWindowTop = value;
        }

        public double SettingWindowLeft
        {
            get => Settings.SettingWindowLeft;
            set => Settings.SettingWindowLeft = value;
        }

        public Brush PreviewBackground
        {
            get
            {
                var wallpaper = WallpaperPathRetrieval.GetWallpaperPath();
                if (wallpaper != null && File.Exists(wallpaper))
                {
                    var memStream = new MemoryStream(File.ReadAllBytes(wallpaper));
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = memStream;
                    bitmap.DecodePixelWidth = 800;
                    bitmap.DecodePixelHeight = 600;
                    bitmap.EndInit();
                    var brush = new ImageBrush(bitmap)
                    {
                        Stretch = Stretch.UniformToFill
                    };
                    return brush;
                }
                else
                {
                    var wallpaperColor = WallpaperPathRetrieval.GetWallpaperColor();
                    var brush = new SolidColorBrush(wallpaperColor);
                    return brush;
                }
            }
        }

        public ResultsViewModel PreviewResults
        {
            get
            {
                var results = new List<Result>
                {
                    new Result
                    {
                        Title = InternationalizationManager.Instance.GetTranslation("SampleTitleExplorer"),
                        SubTitle = InternationalizationManager.Instance.GetTranslation("SampleSubTitleExplorer"),
                        IcoPath = Path.Combine(Constant.ProgramDirectory, @"Plugins\Flow.Launcher.Plugin.Explorer\Images\explorer.png")
                    },
                    new Result
                    {
                        Title = InternationalizationManager.Instance.GetTranslation("SampleTitleWebSearch"),
                        SubTitle = InternationalizationManager.Instance.GetTranslation("SampleSubTitleWebSearch"),
                        IcoPath = Path.Combine(Constant.ProgramDirectory, @"Plugins\Flow.Launcher.Plugin.WebSearch\Images\web_search.png")
                    },
                    new Result
                    {
                        Title = InternationalizationManager.Instance.GetTranslation("SampleTitleProgram"),
                        SubTitle = InternationalizationManager.Instance.GetTranslation("SampleSubTitleProgram"),
                        IcoPath = Path.Combine(Constant.ProgramDirectory, @"Plugins\Flow.Launcher.Plugin.Program\Images\program.png")
                    },
                    new Result
                    {
                        Title = InternationalizationManager.Instance.GetTranslation("SampleTitleProcessKiller"),
                        SubTitle = InternationalizationManager.Instance.GetTranslation("SampleSubTitleProcessKiller"),
                        IcoPath = Path.Combine(Constant.ProgramDirectory, @"Plugins\Flow.Launcher.Plugin.ProcessKiller\Images\app.png")
                    }
                };
                var vm = new ResultsViewModel(Settings);
                vm.AddResults(results, "PREVIEW");
                return vm;
            }
        }

        public FontFamily SelectedQueryBoxFont
        {
            get
            {
                if (Fonts.SystemFontFamilies.Count(o =>
                        o.FamilyNames.Values != null &&
                        o.FamilyNames.Values.Contains(Settings.QueryBoxFont)) > 0)
                {
                    var font = new FontFamily(Settings.QueryBoxFont);
                    return font;
                }
                else
                {
                    var font = new FontFamily("Segoe UI");
                    return font;
                }
            }
            set
            {
                Settings.QueryBoxFont = value.ToString();
                ThemeManager.Instance.ChangeTheme(Settings.Theme);
            }
        }

        public FamilyTypeface SelectedQueryBoxFontFaces
        {
            get
            {
                var typeface = SyntaxSugars.CallOrRescueDefault(
                    () => SelectedQueryBoxFont.ConvertFromInvariantStringsOrNormal(
                        Settings.QueryBoxFontStyle,
                        Settings.QueryBoxFontWeight,
                        Settings.QueryBoxFontStretch
                    ));
                return typeface;
            }
            set
            {
                Settings.QueryBoxFontStretch = value.Stretch.ToString();
                Settings.QueryBoxFontWeight = value.Weight.ToString();
                Settings.QueryBoxFontStyle = value.Style.ToString();
                ThemeManager.Instance.ChangeTheme(Settings.Theme);
            }
        }

        public FontFamily SelectedResultFont
        {
            get
            {
                if (Fonts.SystemFontFamilies.Count(o =>
                        o.FamilyNames.Values != null &&
                        o.FamilyNames.Values.Contains(Settings.ResultFont)) > 0)
                {
                    var font = new FontFamily(Settings.ResultFont);
                    return font;
                }
                else
                {
                    var font = new FontFamily("Segoe UI");
                    return font;
                }
            }
            set
            {
                Settings.ResultFont = value.ToString();
                ThemeManager.Instance.ChangeTheme(Settings.Theme);
            }
        }

        public FamilyTypeface SelectedResultFontFaces
        {
            get
            {
                var typeface = SyntaxSugars.CallOrRescueDefault(
                    () => SelectedResultFont.ConvertFromInvariantStringsOrNormal(
                        Settings.ResultFontStyle,
                        Settings.ResultFontWeight,
                        Settings.ResultFontStretch
                    ));
                return typeface;
            }
            set
            {
                Settings.ResultFontStretch = value.Stretch.ToString();
                Settings.ResultFontWeight = value.Weight.ToString();
                Settings.ResultFontStyle = value.Style.ToString();
                ThemeManager.Instance.ChangeTheme(Settings.Theme);
            }
        }

        public string ThemeImage => Constant.QueryTextBoxIconImagePath;

        #endregion

        #region hotkey

        public CustomPluginHotkey SelectedCustomPluginHotkey { get; set; }

        #endregion

        #region shortcut

        public CustomShortcutModel? SelectedCustomShortcut { get; set; }

        public void DeleteSelectedCustomShortcut()
        {
            var item = SelectedCustomShortcut;
            if (item == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
                return;
            }

            string deleteWarning = string.Format(
                InternationalizationManager.Instance.GetTranslation("deleteCustomShortcutWarning"),
                item.Key, item.Value);
            if (MessageBox.Show(deleteWarning, InternationalizationManager.Instance.GetTranslation("delete"),
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Settings.CustomShortcuts.Remove(item);
            }
        }

        public bool EditSelectedCustomShortcut()
        {
            var item = SelectedCustomShortcut;
            if (item == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
                return false;
            }

            var shortcutSettingWindow = new CustomShortcutSetting(item.Key, item.Value, this);
            if (shortcutSettingWindow.ShowDialog() == true)
            {
                item.Key = shortcutSettingWindow.Key;
                item.Value = shortcutSettingWindow.Value;
                return true;
            }
            return false;
        }

        public void AddCustomShortcut()
        {
            var shortcutSettingWindow = new CustomShortcutSetting(this);
            if (shortcutSettingWindow.ShowDialog() == true)
            {
                var shortcut = new CustomShortcutModel(shortcutSettingWindow.Key, shortcutSettingWindow.Value);
                Settings.CustomShortcuts.Add(shortcut);
            }
        }

        public bool ShortcutExists(string key)
        {
            return Settings.CustomShortcuts.Any(x => x.Key == key) || Settings.BuiltinShortcuts.Any(x => x.Key == key);
        }

        #endregion

        #region about

        public string Website => Constant.Website;
        public string SponsorPage => Constant.SponsorPage;
        public string ReleaseNotes => _updater.GitHubRepository + @"/releases/latest";
        public string Documentation => Constant.Documentation;
        public string Docs => Constant.Docs;
        public string Github => Constant.GitHub;
        public string Version
        {
            get
            {
                if (Constant.Version == "1.0.0")
                {
                    return Constant.Dev;
                }
                else
                {
                    return Constant.Version;
                }
            }
        }
        public string ActivatedTimes => string.Format(_translater.GetTranslation("about_activate_times"), Settings.ActivateTimes);

        public string CheckLogFolder
        {
            get
            {
                var dirInfo = new DirectoryInfo(Path.Combine(DataLocation.DataDirectory(), Constant.Logs, Constant.Version));
                long size = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

                return _translater.GetTranslation("clearlogfolder") + " (" + FormatBytes(size) + ")";
            }
        }

        internal void ClearLogFolder()
        {
            var directory = new DirectoryInfo(
                Path.Combine(
                    DataLocation.DataDirectory(),
                    Constant.Logs,
                    Constant.Version));

            directory.EnumerateFiles()
                .ToList()
                .ForEach(x => x.Delete());
        }
        internal string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new string[]
            {
                "GB", "MB", "KB", "Bytes"
            };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);

                max /= scale;
            }
            return "0 Bytes";
        }

        #endregion
    }
}
