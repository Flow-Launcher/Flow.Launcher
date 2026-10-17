using System;

namespace Flow.Launcher.Plugin.Calculator.Storage;

/// <summary>
/// Represents a calculation that is currently being typed and is waiting to be
/// committed to the history after the debounce delay.
/// </summary>
public sealed record PendingHistoryItem
(
    string Query,
    string CalcResult,
    DateTime CalculatedAt
);
