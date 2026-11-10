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
        if (!Helper.SingleInstance.TryAcquire(args))
        {
            return;
        }

        // Publishes Avalonia's per-pass timing histograms so RenderTrace (⌘⌥⇧R) can record them. Must be set
        // before any Avalonia code runs; recording into a histogram with no listener is a no-op-cheap call.
        AppContext.SetSwitch("Avalonia.Diagnostics.Diagnostic.IsEnabled", true);
        InitializeWpfApplication();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Hosts a WPF Application for legacy plugins that rely on Application.Current.Resources; Windows only.
    static partial void InitializeWpfApplication();

    /// <summary>
    /// Reverse-DNS application id on Linux: X11 WM_CLASS, desktop entry name and xdg-desktop-portal app id.
    /// </summary>
    internal const string LinuxAppId = "com.flowlauncher.FlowLauncher";

    // Avalonia configuration, don't remove; also used by visual designer.
    // macOS: ShowInDock = false, because Avalonia otherwise sets the Regular activation policy, overriding the
    // bundle's LSUIElement and showing a Dock icon (the launcher is a menu-bar/tray app); DisableSetProcessName
    // keeps the bundle's CFBundleName instead of Avalonia's default "Avalonia Application". Ignored elsewhere.
    // RenderingMode: OpenGL until AvaloniaUI/Avalonia#22424 is fixed. On Avalonia 12's default Metal backend, a
    // SizeToContent window that resizes twice in quick succession (results arriving in batches) keeps drawing at
    // the intermediate size, so AppKit shows the content stretched/squashed until the next resize (see also
    // #21477, #20971). OpenGL and Software are unaffected.
    // Linux: WM_CLASS is the desktop entry's app id (Helper/LinuxDesktopEntry), so desktops match windows to the
    // entry (name, icon) and compositor window rules can target the class.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = false, DisableSetProcessName = true })
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software]
            })
            .With(new X11PlatformOptions { WmClass = LinuxAppId })
            .WithInterFont()
            .LogToTrace();
}
