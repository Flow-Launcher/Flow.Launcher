namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Start-at-login registration. Windows: HKCU Run key or a logon scheduled task (AutoStartup.Windows.cs);
/// macOS: a per-user LaunchAgent, which both Windows modes map to (AutoStartup.CrossPlatform.cs).
/// All methods throw when the registration cannot be changed.
/// </summary>
internal static partial class AutoStartup
{
    private static readonly string ClassName = nameof(AutoStartup);

    /// <summary>
    /// Ensures the selected mechanism is registered for the current executable, repairing stale entries.
    /// </summary>
    internal static partial void CheckIsEnabled(bool useLogonTaskForStartup);

    internal static partial void DisableViaLogonTaskAndRegistry();

    internal static partial void ChangeToViaLogonTask();

    internal static partial void ChangeToViaRegistry();
}
