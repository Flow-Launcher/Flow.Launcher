using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
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
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.Threading;

namespace Flow.Launcher
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        #region Public Properties

        public static IPublicAPI API { get; private set; }
        public static bool LoadingOrExiting => _mainWindow == null || _mainWindow.CanClose;

        #endregion

        #region Private Fields

        private static readonly string ClassName = nameof(App);

        private static bool _disposed;
        private static Settings _settings;
        private static MainWindow _mainWindow;
        private readonly MainViewModel _mainVM;
        private readonly Internationalization _internationalization;

        // To prevent two disposals running at the same time.
        private static readonly object _disposingLock = new();

        #endregion

        #region Constructor

        public App()
        {
            // Initialize settings
            _settings.WMPInstalled = WindowsMediaPlayerHelper.IsWindowsMediaPlayerInstalled();

            // Configure the dependency injection container
            try
            {
                var host = Host.CreateDefaultBuilder()
                    .UseContentRoot(AppContext.BaseDirectory)
                    .ConfigureServices(services => services
                        .AddSingleton(_ => _settings)
                        .AddSingleton(sp => new Updater(sp.GetRequiredService<IPublicAPI>(), Launcher.Properties.Settings.Default.GithubRepo))
                        .AddSingleton<Portable>()
                        .AddSingleton<IAlphabet, PinyinAlphabet>()
                        .AddSingleton<StringMatcher>()
                        .AddSingleton<Internationalization>()
                        .AddSingleton<IPublicAPI, PublicAPIInstance>()
                        .AddSingleton<Theme>()
                        // Use one instance for main window view model because we only have one main window
                        .AddSingleton<MainViewModel>()
                        // Use one instance for welcome window view model & setting window view model because
                        // pages in welcome window & setting window need to share the same instance and
                        // these two view models do not need to be reset when creating new windows
                        .AddSingleton<WelcomeViewModel>()
                        .AddSingleton<SettingWindowViewModel>()
                        // Use transient instance for setting window page view models because
                        // pages in setting window need to be recreated when setting window is closed
                        .AddTransient<SettingsPaneAboutViewModel>()
                        .AddTransient<SettingsPaneGeneralViewModel>()
                        .AddTransient<SettingsPaneHotkeyViewModel>()
                        .AddTransient<SettingsPanePluginsViewModel>()
                        .AddTransient<SettingsPanePluginStoreViewModel>()
                        .AddTransient<SettingsPaneProxyViewModel>()
                        .AddTransient<SettingsPaneThemeViewModel>()
                        // Use transient instance for dialog view models because
                        // settings will change and we need to recreate them
                        .AddTransient<SelectBrowserViewModel>()
                        .AddTransient<SelectFileManagerViewModel>()
                    ).Build();
                Ioc.Default.ConfigureServices(host.Services);
            }
            catch (Exception e)
            {
                ShowErrorMsgBoxAndFailFast("Cannot configure dependency injection container, please open new issue in Flow.Launcher", e);
                return;
            }

            // Initialize the public API and Settings first
            try
            {
                API = Ioc.Default.GetRequiredService<IPublicAPI>();
                _settings.Initialize();
                _mainVM = Ioc.Default.GetRequiredService<MainViewModel>();
                _internationalization = Ioc.Default.GetRequiredService<Internationalization>();
            }
            catch (Exception e)
            {
                ShowErrorMsgBoxAndFailFast("Cannot initialize api and settings, please open new issue in Flow.Launcher", e);
                return;
            }
        }

        #endregion

        #region Main

        [STAThread]
        public static void Main()
        {
            // Initialize settings so that we can get language code
            try
            {
                var storage = new FlowLauncherJsonStorage<Settings>();
                _settings = storage.Load();
                _settings.SetStorage(storage);
            }
            catch (Exception e)
            {
                ShowErrorMsgBoxAndFailFast("Cannot load setting storage, please check local data directory", e);
                return;
            }

            // Initialize system language before changing culture info
            Internationalization.InitSystemLanguageCode();

            // Change culture info before application creation to localize WinForm windows
            if (_settings.Language != Constant.SystemLanguageCode)
            {
                Internationalization.ChangeCultureInfo(_settings.Language);
            }

            // Start the application as a single instance
            if (SingleInstance<App>.InitializeAsFirstInstance())
            {
                using var application = new App();
                application.InitializeComponent();
                application.Run();
            }
        }

        #endregion

        #region Fail Fast

        private static void ShowErrorMsgBoxAndFailFast(string message, Exception e)
        {
            // Firstly show users the message
            MessageBox.Show(e.ToString(), message, MessageBoxButton.OK, MessageBoxImage.Error);

            // Flow cannot construct its App instance, so ensure Flow crashes w/ the exception info.
            Environment.FailFast(message, e);
        }

        #endregion

        #region App Events

#pragma warning disable VSTHRD100 // Avoid async void methods

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await API.StopwatchLogInfoAsync(ClassName, "Startup cost", async () =>
            {
                // Because new message box api uses MessageBoxEx window,
                // if it is created and closed before main window is created, it will cause the application to exit.
                // So set to OnExplicitShutdown to prevent the application from shutting down before main window is created
                Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Update dynamic resources base on settings
                Current.Resources["SettingWindowFont"] = new FontFamily(_settings.SettingWindowFont);
                Current.Resources["ContentControlThemeFontFamily"] = new FontFamily(_settings.SettingWindowFont);

                Notification.Install();

                // Enable Win32 dark mode if the system is in dark mode before creating all windows
                Win32Helper.EnableWin32DarkMode(_settings.ColorScheme);

                // Initialize language before portable clean up since it needs translations
                await _internationalization.InitializeLanguageAsync();

                // Clean up after portability updates
                Ioc.Default.GetRequiredService<Portable>().PreStartCleanUpAfterPortabilityUpdate();

                // Initialize logger after data path initialization during portable clean up to ensure correct log directory path is used
                Log.SetLogLevel(_settings.LogLevel);

                API.LogInfo(ClassName, "Begin Flow Launcher startup ----------------------------------------------------");
                API.LogInfo(ClassName, $"Runtime info:{ErrorReporting.RuntimeInfo()}");

                RegisterAppDomainExceptions();
                RegisterDispatcherUnhandledException();
                RegisterTaskSchedulerUnhandledException();

                var imageLoadertask = ImageLoader.InitializeAsync();

                AbstractPluginEnvironment.PreStartPluginExecutablePathUpdate(_settings);

                PluginManager.LoadPlugins(_settings.PluginSettings);

                // Register ResultsUpdated event after all plugins are loaded
                Ioc.Default.GetRequiredService<MainViewModel>().RegisterResultsUpdatedEvent();

                Http.Proxy = _settings.Proxy;

                // Initialize plugin manifest before initializing plugins so that they can use the manifest instantly
                await API.UpdatePluginManifestAsync();

                await PluginManager.InitializePluginsAsync();

                // Update plugin titles after plugins are initialized with their api instances
                Internationalization.UpdatePluginMetadataTranslations();

                await imageLoadertask;

                _mainWindow = new MainWindow();

                Current.MainWindow = _mainWindow;
                Current.MainWindow.Title = Constant.FlowLauncher;

                // Initialize hotkey mapper instantly after main window is created because
                // it will steal focus from main window which causes window hide
                HotKeyMapper.Initialize();

                // Initialize theme for main window
                Ioc.Default.GetRequiredService<Theme>().ChangeTheme();

                DialogJump.InitializeDialogJump(PluginManager.GetDialogJumpExplorers(), PluginManager.GetDialogJumpDialogs());
                DialogJump.SetupDialogJump(_settings.EnableDialogJump);

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                RegisterExitEvents();

                AutoStartup();
                AutoUpdates();
                AutoPluginUpdates();

                API.SaveAppAllSettings();
                API.LogInfo(ClassName, "End Flow Launcher startup ----------------------------------------------------");
            });
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        /// <summary>
        /// Check startup only for Release
        /// </summary>
        [Conditional("RELEASE")]
        private static void AutoStartup()
        {
            // we try to enable auto-startup on first launch, or reenable if it was removed
            // but the user still has the setting set
            if (_settings.StartFlowLauncherOnSystemStartup)
            {
                try
                {
                    Helper.AutoStartup.CheckIsEnabled(_settings.UseLogonTaskForStartup);
                }
                catch (Exception e)
                {
                    // but if it fails (permissions, etc) then don't keep retrying
                    // this also gives the user a visual indication in the Settings widget
                    _settings.StartFlowLauncherOnSystemStartup = false;
                    API.ShowMsgError(API.GetTranslation("setAutoStartFailed"), e.Message);
                }
            }
        }

        [Conditional("RELEASE")]
        private static void AutoUpdates()
        {
            _ = Task.Run(async () =>
            {
                if (_settings.AutoUpdates)
                {
                    // check update every 5 hours
                    var timer = new PeriodicTimer(TimeSpan.FromHours(5));
                    await Ioc.Default.GetRequiredService<Updater>().UpdateAppAsync();

                    while (await timer.WaitForNextTickAsync())
                        // check updates on startup
                        await Ioc.Default.GetRequiredService<Updater>().UpdateAppAsync();
                }
            });
        }

        [Conditional("RELEASE")]
        private static void AutoPluginUpdates()
        {
            _ = Task.Run(async () =>
            {
                if (_settings.AutoUpdatePlugins)
                {
                    // check plugin updates every 5 hour
                    var timer = new PeriodicTimer(TimeSpan.FromHours(5));
                    await PluginInstaller.CheckForPluginUpdatesAsync((plugins) =>
                    {
                        Current.Dispatcher.Invoke(() =>
                        {
                            var pluginUpdateWindow = new PluginUpdateWindow(plugins);
                            pluginUpdateWindow.ShowDialog();
                        });
                    });

                    while (await timer.WaitForNextTickAsync())
                        // check updates on startup
                        await PluginInstaller.CheckForPluginUpdatesAsync((plugins) =>
                        {
                            Current.Dispatcher.Invoke(() =>
                            {
                                var pluginUpdateWindow = new PluginUpdateWindow(plugins);
                                pluginUpdateWindow.ShowDialog();
                            });
                        });
                }
            });
        }

        #endregion

        #region Register Events

        private void RegisterExitEvents()
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                API.LogInfo(ClassName, "Process Exit");
                Dispose();
            };

            Current.Exit += (s, e) =>
            {
                API.LogInfo(ClassName, "Application Exit");
                Dispose();
            };

            Current.SessionEnding += (s, e) =>
            {
                API.LogInfo(ClassName, "Session Ending");
                Dispose();
            };
        }

        /// <summary>
        /// Let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private void RegisterDispatcherUnhandledException()
        {
            DispatcherUnhandledException += ErrorReporting.DispatcherUnhandledException;
        }

        /// <summary>
        /// Let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
        private static void RegisterAppDomainExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += ErrorReporting.UnhandledException;
        }

        /// <summary>
        /// Let exception throw as normal is better for Debug
        /// </summary>
        private static void RegisterTaskSchedulerUnhandledException()
        {
            TaskScheduler.UnobservedTaskException += ErrorReporting.TaskSchedulerUnobservedTaskException;
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            // Prevent two disposes at the same time.
            lock (_disposingLock)
            {
                if (!disposing)
                {
                    return;
                }

                if (_disposed)
                {
                    return;
                }

                // If we call Environment.Exit(0), the application dispose will be called before _mainWindow.Close()
                // Accessing _mainWindow?.Dispatcher will cause the application stuck
                // So here we need to check it and just return so that we will not acees _mainWindow?.Dispatcher
                if (!_mainWindow.CanClose)
                {
                    return;
                }

                _disposed = true;
            }

            API.StopwatchLogInfo(ClassName, "Dispose cost", () =>
            {
                API.LogInfo(ClassName, "Begin Flow Launcher dispose ----------------------------------------------------");

                if (disposing)
                {
                    // Dispose needs to be called on the main Windows thread,
                    // since some resources owned by the thread need to be disposed.
                    _mainWindow?.Dispatcher.Invoke(_mainWindow.Dispose);
                    _mainVM?.Dispose();
                    DialogJump.Dispose();
                    _internationalization.Dispose();
                }

                API.LogInfo(ClassName, "End Flow Launcher dispose ----------------------------------------------------");
            });
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region ISingleInstanceApp

        public void OnSecondAppStarted()
        {
            API.ShowMainWindow();
        }

        #endregion
    }
}
