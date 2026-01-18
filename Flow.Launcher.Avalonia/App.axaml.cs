using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Avalonia.Views.SettingPages;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Flow.Launcher.Avalonia;

public partial class App : Application
{
    private static readonly string ClassName = nameof(App);
    private Settings? _settings;
    private MainViewModel? _mainVM;

    public static IPublicAPI? API { get; private set; }

    public override void Initialize()
    {
        // Configure DI before loading XAML so markup extensions can access services
        LoadSettings();
        ConfigureDI();
        
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            API = Ioc.Default.GetRequiredService<IPublicAPI>();
            _mainVM = Ioc.Default.GetRequiredService<MainViewModel>();

            desktop.MainWindow = new MainWindow();
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize hotkeys after window is created
            HotKeyMapper.Initialize();

            Dispatcher.UIThread.Post(async () => await InitializePluginsAsync(), DispatcherPriority.Background);

            // Cleanup on exit
            desktop.Exit += (_, _) => HotKeyMapper.Shutdown();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        _mainVM?.ToggleFlowLauncher();
    }

    private void MenuShow_OnClick(object? sender, EventArgs e)
    {
        _mainVM?.Show();
    }

    private void MenuSettings_OnClick(object? sender, EventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show();
    }

    private void MenuExit_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

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
            _settings = new Settings
            {
                WindowSize = 580, WindowHeightSize = 42, QueryBoxFontSize = 24,
                ItemHeightSize = 50, ResultItemFontSize = 14, ResultSubItemFontSize = 12, MaxResultsToShow = 6
            };
        }
    }

    private void ConfigureDI()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_settings!);
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
            Log.Info(ClassName, "Loading plugins...");
            PluginManager.LoadPlugins(_settings!.PluginSettings);
            Log.Info(ClassName, $"Loaded {PluginManager.AllPlugins.Count} plugins");

            await PluginManager.InitializePluginsAsync();
            Log.Info(ClassName, "Plugins initialized");

            // Update plugin translations after they are initialized
            var i18n = Ioc.Default.GetRequiredService<Internationalization>();
            i18n.UpdatePluginMetadataTranslations();

            _mainVM?.OnPluginsReady();
        }
        catch (Exception e) { Log.Exception(ClassName, "Plugin init failed", e); }
    }
}
