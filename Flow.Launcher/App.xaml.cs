using System;
using System.Diagnostics;
using System.Text;
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
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Stopwatch = Flow.Launcher.Infrastructure.Stopwatch;

namespace Flow.Launcher
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        public static IPublicAPI API { get; private set; }
        private const string Unique = "Flow.Launcher_Unique_Application_Mutex";
        private static bool _disposed;
        private Settings _settings;
        private MainViewModel _mainVM;
        private SettingWindowViewModel _settingsVM;
        private IStringMatcher _stringMatcher;
        private IUpdater _updater;

        private IServiceProvider _serviceProvider;


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

        private async void OnStartupAsync(object sender, StartupEventArgs e)
        {
            await Stopwatch.NormalAsync("|App.OnStartup|Startup cost", async () =>
            {
                ServiceCollection services = new ServiceCollection();
                ConfigureService(services);
                _serviceProvider = services.BuildServiceProvider();

                _updater = _serviceProvider.GetRequiredService<IUpdater>();

                Log.Info("|App.OnStartup|Begin Flow Launcher startup ----------------------------------------------------");
                Log.Info($"|App.OnStartup|Runtime info:{ErrorReporting.RuntimeInfo()}");
                RegisterAppDomainExceptions();
                RegisterDispatcherUnhandledException();

                ImageLoader.Initialize();

                _settings = _serviceProvider.GetRequiredService<Settings>();

                PluginManager.LoadPlugins(_settings.PluginSettings);

                API = _serviceProvider.GetRequiredService<IPublicAPI>();


                Http.API = API;
                Http.Proxy = _settings.Proxy;

                await PluginManager.InitializePlugins(API);

                Log.Info($"|App.OnStartup|Dependencies Info:{ErrorReporting.DependenciesInfo()}");

                _mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
                var window = _serviceProvider.GetRequiredService<MainWindow>();

                Current.MainWindow = window;
                Current.MainWindow.Title = Constant.FlowLauncher;

                // happlebao todo temp fix for instance code logic
                // load plugin before change language, because plugin language also needs be changed

                var i18N = _serviceProvider.GetRequiredService<II18N>();
                // need to be accessed after Plugin Load
                i18N.LoadLanguageDirectory();
                i18N.ChangeLanguage(_settings.Language);

                // Assign for Compatible access
                InternationalizationManager.Instance = i18N;


                // main windows needs initialized before theme change because of blur settigns
                ThemeManager.Instance.Settings = _settings;
                ThemeManager.Instance.ChangeTheme(_settings.Theme);

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                RegisterExitEvents();

                AutoStartup();
                AutoUpdates();


                _mainVM.MainWindowVisibility = _settings.HideOnStartup ? Visibility.Hidden : Visibility.Visible;
                Log.Info("|App.OnStartup|End Flow Launcher startup ----------------------------------------------------  ");
            });
        }

        private static void ConfigureService(ServiceCollection services)
        {
            services.AddSingleton<FlowLauncherJsonStorage<Settings>>()
                .AddSingleton<Settings>(serviceProvider => serviceProvider.GetRequiredService<FlowLauncherJsonStorage<Settings>>().Load())
                .AddSingleton<IAlphabet, PinyinAlphabet>()
                .AddSingleton<IStringMatcher, FuzzyStringMatcher>()
                .AddSingleton<MainViewModel>()
                .AddSingleton<IUpdater, Updater>(serviceProvider => new Updater(
                    Flow.Launcher.Properties.Settings.Default.GithubRepo,
                    serviceProvider.GetRequiredService<II18N>()))
                .AddSingleton<IPortable, Portable>(_ =>
                {
                    var portable = new Portable();
                    portable.PreStartCleanUpAfterPortabilityUpdate();
                    return portable;
                }).AddSingleton<SettingWindowViewModel>()
                .AddSingleton<MainViewModel>()
                .AddSingleton<MainWindow>()
                .AddSingleton<II18N, Internationalization>()
                .AddSingleton<IPublicAPI, PublicAPIInstance>();

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
                        await _updater.UpdateApp(API);
                    };
                    timer.Start();

                    // check updates on startup
                    await _updater.UpdateApp(API);
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