using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.Views.Dialogs;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Avalonia.Views.SettingPages;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Avalonia;

public partial class App : Application
{
    private static readonly string ClassName = nameof(App);
    private Settings? _settings;
    private MainViewModel? _mainVM;
    private MainWindow? _mainWindow;

    public static IPublicAPI? API { get; private set; }

    public override void Initialize()
    {
        // Configure DI before loading XAML so markup extensions can access services
        LoadSettings();
        ConfigureDI();

        AvaloniaXamlLoader.Load(this);

        // Inject translations into Application.Resources for DynamicResource bindings in plugins
        var i18n = Ioc.Default.GetRequiredService<Internationalization>();
        i18n.InjectIntoApplicationResources();

        #if DEBUG
            this.AttachDeveloperTools();
        #endif

        RenderDiagnosticsHotkey.Register();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            API = Ioc.Default.GetRequiredService<IPublicAPI>();
            _mainVM = Ioc.Default.GetRequiredService<MainViewModel>();

            // Setup log level and dependency-injected settings services before anything else logs or queries.
            _settings!.Initialize();
            Log.SetLogLevel(_settings.LogLevel);
            Http.Proxy = _settings.Proxy;

            _mainWindow = new MainWindow();
            // desktop.MainWindow = _mainWindow; // Prevent auto-show on startup
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize hotkeys after window is created
            HotKeyMapper.Initialize();

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            Dispatcher.UIThread.UnhandledException += OnUiUnhandledException;

            InitializeTrayIcon();
            _settings.PropertyChanged += Settings_PropertyChanged;

            AutoStartup();

            SingleInstance.StartListening(() => Dispatcher.UIThread.Post(() => API?.ShowMainWindow()));

            Dispatcher.UIThread.Post(async () => await InitializePluginsAsync(), DispatcherPriority.Background);

            // Decode the fallback result icon off the UI thread so the first realised row doesn't block on it.
            _ = Task.Run(() => _ = ImageLoader.DefaultImage);

            desktop.ShutdownRequested += (_, e) =>
            {
                if (_exitTask is { IsCompleted: true })
                {
                    return;
                }

                // Finish saving and disposing plugins before letting the lifetime shut down.
                e.Cancel = true;
                _ = ShutdownAfterExitAsync(desktop);
            };

            desktop.Exit += (_, _) =>
            {
                Log.Info(ClassName, "Application Exit");
                WaitForExit();
                _settings.PropertyChanged -= Settings_PropertyChanged;
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                Dispatcher.UIThread.UnhandledException -= OnUiUnhandledException;
            };

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                Log.Info(ClassName, "Process Exit");
                WaitForExit();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Check startup only for Release.
    /// </summary>
    [Conditional("RELEASE")]
    private void AutoStartup()
    {
        if (_settings?.StartFlowLauncherOnSystemStartup != true)
        {
            return;
        }

        try
        {
            Helper.AutoStartup.CheckIsEnabled(_settings.UseLogonTaskForStartup);
        }
        catch (Exception e)
        {
            _settings.StartFlowLauncherOnSystemStartup = false;
            _settings.Save();
            API?.ShowMsgError(Translator.GetString("setAutoStartFailed"), e.Message);
        }
    }

    [Conditional("RELEASE")]
    private void AutoPluginUpdates()
    {
        if (_settings?.AutoUpdatePlugins != true)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            // Check plugin updates on startup and then every 5 hours
            using var timer = new PeriodicTimer(TimeSpan.FromHours(5));
            do
            {
                try
                {
                    await PluginInstaller.CheckForPluginUpdatesAsync(
                        plugins => Dispatcher.UIThread.InvokeAsync(() => ShowPluginUpdateWindowAsync(plugins)));
                }
                catch (Exception e)
                {
                    Log.Exception(ClassName, "Failed to check plugin updates", e);
                }
            } while (await timer.WaitForNextTickAsync());
        });
    }

    private static async Task ShowPluginUpdateWindowAsync(List<PluginUpdateInfo> plugins)
    {
        try
        {
            if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            {
                return;
            }

            var dialog = new PluginUpdateWindow(plugins);
            await dialog.ShowDialog<bool>(owner);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Failed to show plugin update window", e);
        }
    }

    private void ShowReleaseNotesNotification()
    {
        // Skip release notes notification for developer builds (version 1.0.0)
        if (Constant.Version == "1.0.0" || _settings!.ReleaseNotesVersion == Constant.Version)
        {
            return;
        }

        var firstLaunch = string.IsNullOrEmpty(_settings.ReleaseNotesVersion);
        _settings.ReleaseNotesVersion = Constant.Version;
        if (firstLaunch)
        {
            return;
        }

        API!.ShowMsgWithButton(
            string.Format(API.GetTranslation("appUpdateTitle"), Constant.Version),
            API.GetTranslation("appUpdateButtonContent"),
            () => Dispatcher.UIThread.Post(() => new ReleaseNotesWindow().Show()));
    }

    #region Tray icon

    private TrayIcon? _trayIcon;
    private NativeMenuItem? _trayOpen;
    private NativeMenuItem? _trayGameMode;
    private NativeMenuItem? _trayPositionReset;
    private NativeMenuItem? _traySettings;
    private NativeMenuItem? _trayExit;

    private void InitializeTrayIcon()
    {
        _trayIcon = TrayIcon.GetIcons(this)?.Count > 0 ? TrayIcon.GetIcons(this)![0] : null;
        if (_trayIcon is null)
        {
            return;
        }

        _trayOpen = new NativeMenuItem();
        _trayOpen.Click += (_, _) => _mainVM?.ToggleFlowLauncher();
        var gameMode = new NativeMenuItem { ToggleType = MenuItemToggleType.CheckBox };
        gameMode.Click += (_, _) =>
        {
            API?.ToggleGameMode();
            gameMode.IsChecked = API?.IsGameModeOn() == true;
        };
        _trayGameMode = gameMode;
        _trayPositionReset = new NativeMenuItem();
        _trayPositionReset.Click += (_, _) => _mainWindow?.ResetPosition();
        _traySettings = new NativeMenuItem();
        _traySettings.Click += (_, _) => SettingsWindow.Open();
        _trayExit = new NativeMenuItem();
        _trayExit.Click += (_, _) =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _ = ShutdownAfterExitAsync(desktop);
            }
        };

        // Populate the menu declared in App.axaml: replacing TrayIcon.Menu after startup trips
        // Avalonia.Native's menu exporter ("The menu being updated does not match").
        var menu = _trayIcon.Menu ?? new NativeMenu();
        UpdateTrayIconText();
        menu.Items.Clear();
        menu.Items.Add(_trayOpen);
        menu.Items.Add(_trayGameMode);
        menu.Items.Add(_trayPositionReset);
        menu.Items.Add(_traySettings);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_trayExit);
        // Game mode may be toggled by hotkey; refresh the check mark whenever the menu is about to open.
        menu.NeedsUpdate += (_, _) => gameMode.IsChecked = API?.IsGameModeOn() == true;

        if (_trayIcon.Menu is null)
        {
            _trayIcon.Menu = menu;
        }
        _trayIcon.ToolTipText = Constant.FlowLauncherFullName;
        _trayIcon.IsVisible = !_settings!.HideNotifyIcon;
        UpdateTrayIconText();
    }

    private void UpdateTrayIconText()
    {
        if (API is null || _trayOpen is null)
        {
            return;
        }

        _trayOpen.Header = API.GetTranslation("iconTrayOpen") + " (" + _settings!.Hotkey + ")";
        _trayGameMode!.Header = API.GetTranslation("GameMode");
        _trayGameMode.ToolTip = API.GetTranslation("GameModeToolTip");
        _trayGameMode.IsChecked = API.IsGameModeOn();
        _trayPositionReset!.Header = API.GetTranslation("PositionReset");
        _trayPositionReset.ToolTip = API.GetTranslation("PositionResetToolTip");
        _traySettings!.Header = API.GetTranslation("iconTraySettings");
        _trayExit!.Header = API.GetTranslation("iconTrayExit");
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Settings.Language):
            case nameof(Settings.Hotkey):
                Dispatcher.UIThread.Post(UpdateTrayIconText);
                break;
            case nameof(Settings.HideNotifyIcon):
                Dispatcher.UIThread.Post(() =>
                {
                    if (_trayIcon is not null)
                    {
                        _trayIcon.IsVisible = !_settings!.HideNotifyIcon;
                    }
                });
                break;
        }
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        _mainVM?.ToggleFlowLauncher();
    }

    #endregion

    #region Exit

    private static readonly TimeSpan PluginDisposeTimeout = TimeSpan.FromSeconds(5);
    private readonly object _exitLock = new();
    private Task? _exitTask;

    /// <summary>
    /// Saves settings, stops hotkeys, disposes plugins (bounded) and releases the single-instance lock. Idempotent.
    /// </summary>
    private Task ExitAsync()
    {
        lock (_exitLock)
        {
            return _exitTask ??= ExitCoreAsync();
        }
    }

    private async Task ExitCoreAsync()
    {
        Log.Info(ClassName, "Begin Flow Launcher exit");
        try { API?.SaveAppAllSettings(); }
        catch (Exception e) { Log.Exception(ClassName, "Failed to save settings on exit", e); }

        try { HotKeyMapper.Shutdown(); }
        catch (Exception e) { Log.Exception(ClassName, "Failed to shut down hotkeys", e); }

        try
        {
            var dispose = Task.Run(async () => await PluginManager.DisposePluginsAsync());
            if (await Task.WhenAny(dispose, Task.Delay(PluginDisposeTimeout)).ConfigureAwait(false) != dispose)
            {
                Log.Warn(ClassName, "Plugin dispose timed out");
            }
        }
        catch (Exception e) { Log.Exception(ClassName, "Failed to dispose plugins", e); }

        SingleInstance.Release();
        Log.Info(ClassName, "End Flow Launcher exit");
    }

    private async Task ShutdownAfterExitAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        await ExitAsync();
        Dispatcher.UIThread.Post(() => desktop.Shutdown());
    }

    /// <summary>
    /// Synchronous, bounded exit for Exit/ProcessExit handlers; ExitCoreAsync never resumes on the UI context, so blocking here is safe.
    /// </summary>
    private void WaitForExit()
    {
        try { ExitAsync().Wait(PluginDisposeTimeout + TimeSpan.FromSeconds(1)); }
        catch (Exception e) { Log.Exception(ClassName, "Exit failed", e); }
    }

    #endregion

    private void LoadSettings()
    {
        try
        {
            var storage = new FlowLauncherJsonStorage<Settings>();
            _settings = storage.Load();
            _settings.SetStorage(storage);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Settings load failed", e);
            var storage = new FlowLauncherJsonStorage<Settings>();
            _settings = new Settings
            {
                WindowSize = 580, WindowHeightSize = 42, QueryBoxFontSize = 24,
                ItemHeightSize = 50, ResultItemFontSize = 14, ResultSubItemFontSize = 12, MaxResultsToShow = 6
            };
            _settings.SetStorage(storage);
        }
    }

    private void ConfigureDI()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_settings!);
        services.AddSingleton(sp => new Updater(sp.GetRequiredService<IPublicAPI>(), Constant.GitHub));
        services.AddSingleton<Portable>();
        services.AddSingleton<IAlphabet, PinyinAlphabet>();
        services.AddSingleton<StringMatcher>();
        services.AddSingleton<Internationalization>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<IPublicAPI>(sp => new AvaloniaPublicAPI(
            sp.GetRequiredService<Settings>(),
            () => sp.GetRequiredService<MainViewModel>(),
            sp.GetRequiredService<Internationalization>()));
        Ioc.Default.ConfigureServices(services.BuildServiceProvider());
    }

    private async Task InitializePluginsAsync()
    {
        try
        {
            // Initialize plugin manifest before initializing plugins so that they can use the manifest instantly
            try
            {
                await API!.UpdatePluginManifestAsync();
            }
            catch (Exception e)
            {
                Log.Exception(ClassName, "Plugin manifest update failed", e);
            }

            Log.Info(ClassName, "Loading plugins...");
            // Plugin discovery, environment probing and assembly loading touch no UI objects; keep them off the UI thread.
            var pluginSettings = _settings!.PluginSettings;
            await Task.Run(() => PluginManager.LoadPlugins(pluginSettings));
            Log.Info(ClassName, $"Loaded {PluginManager.GetAllLoadedPlugins().Count} plugins");

            await PluginManager.InitializePluginsAsync(_mainVM!);
            Log.Info(ClassName, "Plugins initialized");

            // Update plugin translations after they are initialized
            var i18n = Ioc.Default.GetRequiredService<Internationalization>();
            i18n.UpdatePluginMetadataTranslations();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = _mainWindow;
            }

            _mainVM?.OnPluginsReady();

            ShowReleaseNotesNotification();
            AutoPluginUpdates();

            // Save all settings since plugin init may update plugin environment paths
            API!.SaveAppAllSettings();
        }
        catch (Exception e) { Log.Exception(ClassName, "Plugin init failed", e); }
    }

    private void OnUiUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Exception(ClassName, "Unhandled UI exception", e.Exception);
        ShowReportWindow(e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Exception(ClassName, "Unhandled exception", exception);
            ShowReportWindow(exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Exception(ClassName, "Unobserved task exception occurred.", e.Exception);
        e.SetObserved();
    }

    private static void ShowReportWindow(Exception exception)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = new ReportWindow(exception);
            window.Show();
            window.Activate();
        });
    }
}
