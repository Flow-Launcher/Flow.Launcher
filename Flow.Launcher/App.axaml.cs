using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.ExternalPlugins.Environments;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using Microsoft.VisualStudio.Threading;
using PropertyChanged;
using Application = Avalonia.Application;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher
{
    [DoNotNotify]
    public partial class App : Application, IDisposable, ISingleInstanceApp
    {
        public static PublicAPIInstance API { get; private set; }
        private const string Unique = "Flow.Launcher_Unique_Application_Mutex";
        private static bool _disposed;
        private Settings _settings;
        private MainViewModel _mainVM;
        private SettingWindowViewModel _settingsVM;
        private readonly Updater _updater = new Updater(Flow.Launcher.Properties.Settings.Default.GithubRepo);
        private readonly Portable _portable = new Portable();
        private readonly PinyinAlphabet _alphabet = new PinyinAlphabet();
        private StringMatcher _stringMatcher;

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            if (!SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                return;
            }
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }


        public override void OnFrameworkInitializationCompleted()
        {
            new JoinableTaskFactory(new JoinableTaskContext()).Run(() => Stopwatch.NormalAsync(
                "|App.OnStartup|Startup cost",
                async () =>
                {
                    _portable.PreStartCleanUpAfterPortabilityUpdate();

                    Log.Info(
                        "|App.OnStartup|Begin Flow Launcher startup ----------------------------------------------------");
                    Log.Info($"|App.OnStartup|Runtime info:{ErrorReporting.RuntimeInfo()}");
                    RegisterAppDomainExceptions();

                    var imageLoadertask = ImageLoader.InitializeAsync();

                    _settingsVM = new SettingWindowViewModel(_updater, _portable);
                    _settings = _settingsVM.Settings;

                    AbstractPluginEnvironment.PreStartPluginExecutablePathUpdate(_settings);

                    _alphabet.Initialize(_settings);
                    _stringMatcher = new StringMatcher(_alphabet);
                    StringMatcher.Instance = _stringMatcher;
                    _stringMatcher.UserSettingSearchPrecision = _settings.QuerySearchPrecision;

                    PluginManager.LoadPlugins(_settings.PluginSettings);
                    _mainVM = new MainViewModel(_settings);

                    API = new PublicAPIInstance(_settingsVM, _mainVM, _alphabet);

                    Http.API = API;
                    Http.Proxy = _settings.Proxy;

                    await PluginManager.InitializePluginsAsync(API);
                    await imageLoadertask;

                    var window = new MainWindow(_settings, _mainVM);

                    Log.Info($"|App.OnStartup|Dependencies Info:{ErrorReporting.DependenciesInfo()}");

                    if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                        throw new NotSupportedException("desktop is not IClassicDesktopStyleApplicationLifetime");

                    desktop.MainWindow = window;
                    desktop.MainWindow.Title = Constant.FlowLauncher;
                    // desktop.Exit += (s, e) => Dispose();

                    HotKeyMapper.Initialize(_mainVM);

                    // todo temp fix for instance code logic
                    // load plugin before change language, because plugin language also needs be changed
                    InternationalizationManager.Instance.Settings = _settings;
                    InternationalizationManager.Instance.ChangeLanguage(_settings.Language);
                    // main windows needs initialized before theme change because of blur settings
                    ThemeManager.Instance.Settings = _settings;
                    ThemeManager.Instance.ChangeTheme(_settings.Theme);

                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                    RegisterExitEvents();

                    AutoStartup();
                    AutoUpdates();

                    API.SaveAppAllSettings();
                    Log.Info(
                        "|App.OnStartup|End Flow Launcher startup ----------------------------------------------------  ");
                }));
            base.OnFrameworkInitializationCompleted();
        }

        private void AutoStartup()
        {
            // we try to enable auto-startup on first launch, or reenable if it was removed
            // but the user still has the setting set
            if (_settings.StartFlowLauncherOnSystemStartup && !Helper.AutoStartup.IsEnabled)
            {
                try
                {
                    Helper.AutoStartup.Enable();
                }
                catch (Exception e)
                {
                    // but if it fails (permissions, etc) then don't keep retrying
                    // this also gives the user a visual indication in the Settings widget
                    _settings.StartFlowLauncherOnSystemStartup = false;
                    Notification.Show(InternationalizationManager.Instance.GetTranslation("setAutoStartFailed"),
                        e.Message);
                }
            }
        }

        //[Conditional("RELEASE")]
        private void AutoUpdates()
        {
            _ = Task.Run(async () =>
            {
                if (_settings.AutoUpdates)
                {
                    // check update every 5 hours
                    var timer = new PeriodicTimer(TimeSpan.FromHours(5));
                    await _updater.UpdateAppAsync(API);

                    while (await timer.WaitForNextTickAsync())
                        // check updates on startup
                        await _updater.UpdateAppAsync(API);
                }
            });
        }


        private void RegisterExitEvents()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
        }

        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private static void RegisterAppDomainExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledExceptionHandle;
        }

        public void Dispose()
        {
            // if sessionending is called, exit proverbially be called when log off / shutdown
            // but if sessionending is not called, exit won't be called when log off / shutdown
            if (!_disposed)
            {
                API.SaveAppAllSettings();
                _disposed = true;
            }
        }

        public void OnSecondAppStarted()
        {
            _mainVM.Show();
        }
    }
}
