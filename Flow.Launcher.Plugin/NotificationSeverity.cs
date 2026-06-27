namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Specifies the severity level of an inline notification shown in the Flow Launcher window.
    /// </summary>
    public enum NotificationSeverity
    {
        /// <summary>Informational notification (blue icon).</summary>
        Informational = 0,

        /// <summary>Success notification (green icon).</summary>
        Success = 1,

        /// <summary>Warning notification (yellow icon).</summary>
        Warning = 2,

        /// <summary>Error notification (red icon).</summary>
        Error = 3
    }
}
