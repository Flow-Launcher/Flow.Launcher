namespace Flow.Launcher.Plugin;

/// <summary>
/// Enum for search delay time
/// </summary>
public enum SearchDelayTime
{
    /// <summary>
    /// Long search delay time. 250ms.
    /// </summary>
    Long,

    /// <summary>
    /// Moderately long search delay time. 200ms.
    /// </summary>
    ModeratelyLong,

    /// <summary>
    /// Medium search delay time. 150ms. Default value.
    /// </summary>
    Medium,

    /// <summary>
    /// Moderately short search delay time. 100ms.
    /// </summary>
    ModeratelyShort,

    /// <summary>
    /// Short search delay time. 50ms.
    /// </summary>
    Short
}
