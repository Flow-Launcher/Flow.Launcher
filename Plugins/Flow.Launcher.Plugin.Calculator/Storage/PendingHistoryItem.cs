using System;

namespace Flow.Launcher.Plugin.Calculator.Storage;

public sealed record PendingHistoryItem
    (
    Result Result,
    string Expression,
    Func<ActionContext, bool> Action,
    string SubTitle,
    DateTime CalculatedAt
    );
