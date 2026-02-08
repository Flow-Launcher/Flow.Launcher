using System;
using Avalonia;

namespace Flow.Launcher.Avalonia;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Initialize WPF Application for plugins that rely on Application.Current.Resources
        if (System.Windows.Application.Current == null)
        {
            var app = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
            
            // Add common resources expected by plugins
            // We load the copied WPF resources
            try 
            {
                // Load base theme resources (Colors like Color01B, etc.)
                // TODO: Sync this with Avalonia theme (Light/Dark)
                var themeDict = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Flow.Launcher.Avalonia;component/WpfResources/Dark.xaml")
                };
                app.Resources.MergedDictionaries.Add(themeDict);

                var dict = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Flow.Launcher.Avalonia;component/WpfResources/CustomControlTemplate.xaml")
                };
                app.Resources.MergedDictionaries.Add(dict);

                var dict2 = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Flow.Launcher.Avalonia;component/WpfResources/SettingWindowStyle.xaml")
                };
                app.Resources.MergedDictionaries.Add(dict2);
            }
            catch (Exception ex)
            {
                // Fallback if loading fails - at least define the margin that caused the crash
                System.Diagnostics.Debug.WriteLine($"Failed to load WPF resources: {ex}");
                var inner = ex.InnerException;
                while (inner != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner: {inner}");
                    inner = inner.InnerException;
                }

                if (!app.Resources.Contains("SettingPanelMargin"))
                {
                    app.Resources.Add("SettingPanelMargin", new System.Windows.Thickness(70, 13.5, 18, 13.5));
                }
                if (!app.Resources.Contains("SettingPanelItemTopBottomMargin"))
                {
                    app.Resources.Add("SettingPanelItemTopBottomMargin", new System.Windows.Thickness(0, 4.5, 0, 4.5));
                }
                if (!app.Resources.Contains("SettingPanelItemRightMargin"))
                {
                    app.Resources.Add("SettingPanelItemRightMargin", new System.Windows.Thickness(0, 0, 9, 0));
                }
                if (!app.Resources.Contains("SettingPanelItemLeftMargin"))
                {
                    app.Resources.Add("SettingPanelItemLeftMargin", new System.Windows.Thickness(9, 0, 0, 0));
                }
                if (!app.Resources.Contains("SettingPanelItemLeftTopBottomMargin"))
                {
                    app.Resources.Add("SettingPanelItemLeftTopBottomMargin", new System.Windows.Thickness(9, 4.5, 0, 4.5));
                }
                if (!app.Resources.Contains("SettingPanelItemRightTopBottomMargin"))
                {
                    app.Resources.Add("SettingPanelItemRightTopBottomMargin", new System.Windows.Thickness(0, 4.5, 9, 4.5));
                }
            }
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
