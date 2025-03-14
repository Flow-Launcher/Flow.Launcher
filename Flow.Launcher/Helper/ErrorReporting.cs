using System;
using System.Windows.Threading;
using NLog;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Exception;

namespace Flow.Launcher.Helper;

public static class ErrorReporting
{
    private static void Report(Exception e)
    {
        var logger = LogManager.GetLogger("UnHandledException");
        logger.Fatal(ExceptionFormatter.FormatExcpetion(e));
        var reportWindow = new ReportWindow(e);
        reportWindow.Show();
    }

    public static void UnhandledExceptionHandle(object sender, UnhandledExceptionEventArgs e)
    {
        //handle non-ui thread exceptions
        Report((Exception)e.ExceptionObject);
    }

    public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        //handle ui thread exceptions
        Report(e.Exception);
        //prevent application exist, so the user can copy prompted error info
        e.Handled = true;
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
