namespace Flow.Launcher.Plugin;

/// <summary>
/// Enum for search delay time
/// </summary>
public enum SearchDelayTime
{
    /// <summary>
    /// Very long search delay time. 250ms.
    /// </summary>
    VeryLong,

    /// <summary>
    /// Long search delay time. 200ms.
    /// </summary>
    Long,

    /// <summary>
    /// Normal search delay time. 150ms. Default value.
    /// </summary>
    Normal,

    /// <summary>
    /// Short search delay time. 100ms.
    /// </summary>
    Short,

    /// <summary>
    /// Very short search delay time. 50ms.
    /// </summary>
    VeryShort
}
