using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Exception;
using Flow.Launcher.Infrastructure.Logger;
using NLog;

namespace Flow.Launcher.Helper;

public static class ErrorReporting
{
    private static void Report(Exception e, bool silent = false, [CallerMemberName] string methodName = "UnHandledException")
    {
        var logger = LogManager.GetLogger(methodName);
        logger.Fatal(ExceptionFormatter.FormatExcpetion(e));
        if (silent) return;

        // The crash occurs in PresentationFramework.dll, not necessarily when the Runner UI is visible, originating from this line:
        // https://github.com/dotnet/wpf/blob/3439f20fb8c685af6d9247e8fd2978cac42e74ac/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Shell/WindowChromeWorker.cs#L1005
        // Many bug reports because users see the "Report problem UI" after "the" crash with System.Runtime.InteropServices.COMException 0xD0000701 or 0x80263001.
        // However, displaying this "Report problem UI" during WPF crashes, especially when DWM composition is changing, is not ideal; some users reported it hangs for up to a minute before the "Report problem UI" appears.
        // This change modifies the behavior to log the exception instead of showing the "Report problem UI".
        if (ExceptionHelper.IsRecoverableDwmCompositionException(e)) return;

        var reportWindow = new ReportWindow(e);
        reportWindow.Show();
    }

    public static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // handle non-ui thread exceptions
        Report((Exception)e.ExceptionObject);
    }

    public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // handle ui thread exceptions
        Report(e.Exception);
        // prevent application exist, so the user can copy prompted error info
        e.Handled = true;
    }

    public static void TaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        // log exception but do not handle unobserved task exceptions on UI thread
        //Application.Current.Dispatcher.Invoke(() => Report(e.Exception, true));
        Log.Exception(nameof(ErrorReporting), "Unobserved task exception occurred.", e.Exception);
        // prevent application exit, so the user can copy the prompted error info
        e.SetObserved();
    }

    public static string RuntimeInfo()
    {
        var info =
            $"""

             Flow Launcher version: {Constant.Version}
             OS Version: {ExceptionFormatter.GetWindowsFullVersionFromRegistry()}
             IntPtr Length: {IntPtr.Size}
             x64: {Environment.Is64BitOperatingSystem}
             """;
        return info;
    }

    public static string DependenciesInfo()
    {
        var info = $"""

                    Python Path: {Constant.PythonPath}
                    Node Path: {Constant.NodePath}
                    """;
        return info;
    }
}
