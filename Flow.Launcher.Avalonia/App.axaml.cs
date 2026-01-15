using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Flow.Launcher.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Set up dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        Ioc.Default.ConfigureServices(serviceProvider);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register settings - for now create a default instance
        // In production, this would load from the existing settings file
        services.AddSingleton<Settings>(_ => 
        {
            var settings = new Settings();
            // Set some defaults for the Avalonia version
            settings.WindowSize = 580;
            settings.WindowHeightSize = 42;
            settings.QueryBoxFontSize = 24;
            settings.ItemHeightSize = 50;
            settings.ResultItemFontSize = 14;
            settings.ResultSubItemFontSize = 12;
            settings.MaxResultsToShow = 6;
            return settings;
        });
    }
}
