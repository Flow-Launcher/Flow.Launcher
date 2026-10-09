using System;
using Avalonia;

namespace Flow.Launcher.Avalonia;

internal sealed partial class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        InitializeWpfApplication();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Hosts a WPF Application for legacy plugins that rely on Application.Current.Resources; Windows only.
    static partial void InitializeWpfApplication();

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
