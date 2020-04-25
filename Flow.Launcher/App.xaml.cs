using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        public static PublicAPIInstance API { get; private set; }
        private const string Unique = "Flow.Launcher_Unique_Application_Mutex";
        private static bool _disposed;
        private Settings _settings;
        private MainViewModel _mainVM;
        private SettingWindowViewModel _settingsVM;
        private readonly Updater _updater = new Updater(Flow.Launcher.Properties.Settings.Default.GithubRepo);
        private readonly Portable _portable = new Portable();
        private readonly Alphabet _alphabet = new Alphabet();
        private StringMatcher _stringMatcher;

        [STAThread]
        public static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                using (var application = new App())
                {
                    application.InitializeComponent();
                    application.Run();
                }
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Stopwatch.Normal("|App.OnStartup|Startup cost", () =>
            {
                _portable.PreStartCleanUpAfterPortabilityUpdate();

                Log.Info("|App.OnStartup|Begin Flow Launcher startup ----------------------------------------------------");
                Log.Info($"|App.OnStartup|Runtime info:{ErrorReporting.RuntimeInfo()}");
                RegisterAppDomainExceptions();
                RegisterDispatcherUnhandledException();

                ImageLoader.Initialize();

                _settingsVM = new SettingWindowViewModel(_updater, _portable);
                _settings = _settingsVM.Settings;

                _alphabet.Initialize(_settings);
                _stringMatcher = new StringMatcher(_alphabet);
                StringMatcher.Instance = _stringMatcher;
                _stringMatcher.UserSettingSearchPrecision = _settings.QuerySearchPrecision;

                PluginManager.LoadPlugins(_settings.PluginSettings);
                _mainVM = new MainViewModel(_settings);
                var window = new MainWindow(_settings, _mainVM);
                API = new PublicAPIInstance(_settingsVM, _mainVM, _alphabet);
                PluginManager.InitializePlugins(API);
                Log.Info($"|App.OnStartup|Dependencies Info:{ErrorReporting.DependenciesInfo()}");

                Current.MainWindow = window;
                Current.MainWindow.Title = Constant.FlowLauncher;

                // happlebao todo temp fix for instance code logic
                // load plugin before change language, because plugin language also needs be changed
                InternationalizationManager.Instance.Settings = _settings;
                InternationalizationManager.Instance.ChangeLanguage(_settings.Language);
                // main windows needs initialized before theme change because of blur settigns
                ThemeManager.Instance.Settings = _settings;
                ThemeManager.Instance.ChangeTheme(_settings.Theme);

                Http.Proxy = _settings.Proxy;

                RegisterExitEvents();

                AutoStartup();
                AutoUpdates();

                _mainVM.MainWindowVisibility = _settings.HideOnStartup ? Visibility.Hidden : Visibility.Visible;
                Log.Info("|App.OnStartup|End Flow Launcher startup ----------------------------------------------------  ");
            });
        }


        private void AutoStartup()
        {
            if (_settings.StartFlowLauncherOnSystemStartup)
            {
                if (!SettingWindow.StartupSet())
                {
                    SettingWindow.SetStartup();
                }
            }
        }

        //[Conditional("RELEASE")]
        private void AutoUpdates()
        {
            Task.Run(async () =>
            {
                if (_settings.AutoUpdates)
                {
                    // check udpate every 5 hours
                    var timer = new Timer(1000 * 60 * 60 * 5);
                    timer.Elapsed += async (s, e) =>
                    {
                        await _updater.UpdateApp();
                    };
                    timer.Start();

                    // check updates on startup
                    await _updater.UpdateApp();
                }
            });
        }

        private void RegisterExitEvents()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
            Current.Exit += (s, e) => Dispose();
            Current.SessionEnding += (s, e) => Dispose();
        }

        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private void RegisterDispatcherUnhandledException()
        {
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
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
            Current.MainWindow.Visibility = Visibility.Visible;
        }
    }
}