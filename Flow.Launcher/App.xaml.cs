using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flow.Launcher
{
    public partial class App : IDisposable, ISingleInstanceApp
    {
        #region Public Properties

        public static IPublicAPI API { get; private set; }

        #endregion

        #region Private Fields

        private static readonly string ClassName = nameof(App);

        private static bool _disposed;
        private MainWindow _mainWindow;
        private readonly MainViewModel _mainVM;
        private readonly Settings _settings;

        // To prevent two disposals running at the same time.
        private static readonly object _disposingLock = new();

        #endregion

        #region Constructor

        public App()
        {
            // Initialize settings
            try
            {
                var storage = new FlowLauncherJsonStorage<Settings>();
                _settings = storage.Load();
                _settings.SetStorage(storage);
                _settings.WMPInstalled = WindowsMediaPlayerHelper.IsWindowsMediaPlayerInstalled();
            }
            catch (Exception e)
            {
                ShowErrorMsgBoxAndFailFast("Cannot load setting storage, please check local data directory", e);
                return;
            }

            // Configure the dependency injection container
            try
            {
                var host = Host.CreateDefaultBuilder()
                    .UseContentRoot(AppContext.BaseDirectory)
                    .ConfigureServices(services => services
                        .AddSingleton(_ => _settings)
                        .AddSingleton(sp => new Updater(sp.GetRequiredService<IPublicAPI>(), Launcher.Properties.Settings.Default.GithubRepo))
                        .AddSingleton<Portable>()
                        .AddSingleton<SettingWindowViewModel>()
                        .AddSingleton<IAlphabet, PinyinAlphabet>()
                        .AddSingleton<StringMatcher>()
                        .AddSingleton<Internationalization>()
                        .AddSingleton<IPublicAPI, PublicAPIInstance>()
                        .AddSingleton<MainViewModel>()
                        .AddSingleton<Theme>()
                        .AddSingleton<WelcomeViewModel>()
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
            }
            catch (Exception e)
            {
                ShowErrorMsgBoxAndFailFast("Cannot initialize api and settings, please open new issue in Flow.Launcher", e);
                return;
            }

            // Local function
            static void ShowErrorMsgBoxAndFailFast(string message, Exception e)
            {
                // Firstly show users the message
                MessageBox.Show(e.ToString(), message, MessageBoxButton.OK, MessageBoxImage.Error);

                // Flow cannot construct its App instance, so ensure Flow crashes w/ the exception info.
                Environment.FailFast(message, e);
            }
        }

        #endregion

        #region Main

        [STAThread]
        public static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance())
            {
                using var application = new App();
                application.InitializeComponent();
                application.Run();
            }
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

                Log.SetLogLevel(_settings.LogLevel);

                Ioc.Default.GetRequiredService<Portable>().PreStartCleanUpAfterPortabilityUpdate();

                API.LogInfo(ClassName, "Begin Flow Launcher startup ----------------------------------------------------");
                API.LogInfo(ClassName, "Runtime info:{ErrorReporting.RuntimeInfo()}");

                RegisterAppDomainExceptions();
                RegisterDispatcherUnhandledException();
                RegisterTaskSchedulerUnhandledException();

                var imageLoadertask = ImageLoader.InitializeAsync();

                AbstractPluginEnvironment.PreStartPluginExecutablePathUpdate(_settings);

                PluginManager.LoadPlugins(_settings.PluginSettings);

                // Register ResultsUpdated event after all plugins are loaded
                Ioc.Default.GetRequiredService<MainViewModel>().RegisterResultsUpdatedEvent();

                Http.Proxy = _settings.Proxy;

                await PluginManager.InitializePluginsAsync();

                // Change language after all plugins are initialized because we need to update plugin title based on their api
                // TODO: Clean InternationalizationManager.Instance and InternationalizationManager.Instance.GetTranslation in future
                await Ioc.Default.GetRequiredService<Internationalization>().InitializeLanguageAsync();

                await imageLoadertask;

                _mainWindow = new MainWindow();

                API.LogInfo(ClassName, "Dependencies Info:{ErrorReporting.DependenciesInfo()}");

                Current.MainWindow = _mainWindow;
                Current.MainWindow.Title = Constant.FlowLauncher;

                // main windows needs initialized before theme change because of blur settings
                Ioc.Default.GetRequiredService<Theme>().ChangeTheme();

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                RegisterExitEvents();

                AutoStartup();
                AutoUpdates();

                API.SaveAppAllSettings();
                API.LogInfo(ClassName, "End Flow Launcher startup ----------------------------------------------------");
            });
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        private void AutoStartup()
        {
            // we try to enable auto-startup on first launch, or reenable if it was removed
            // but the user still has the setting set
            if (_settings.StartFlowLauncherOnSystemStartup && !Helper.AutoStartup.IsEnabled)
            {
                try
                {
                    if (_settings.UseLogonTaskForStartup)
                    {
                        Helper.AutoStartup.EnableViaLogonTask();
                    }
                    else
                    {
                        Helper.AutoStartup.EnableViaRegistry();
                    }
                }
                catch (Exception e)
                {
                    // but if it fails (permissions, etc) then don't keep retrying
                    // this also gives the user a visual indication in the Settings widget
                    _settings.StartFlowLauncherOnSystemStartup = false;
                    API.ShowMsg(API.GetTranslation("setAutoStartFailed"), e.Message);
                }
            }
        }

        private void AutoUpdates()
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

        /// <summary>
        /// let exception throw as normal is better for Debug
        /// </summary>
        [Conditional("RELEASE")]
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
