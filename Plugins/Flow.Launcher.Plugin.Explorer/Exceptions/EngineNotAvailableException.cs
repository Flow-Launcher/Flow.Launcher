#nullable enable

using System;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.Explorer.Exceptions;

public class EngineNotAvailableException : Exception
{
    public string EngineName { get; }
    public string Resolution { get; }
    public Func<ActionContext, ValueTask<bool>>? Action { get; }
    
    public string? ErrorIcon { get; init; }
    
    public EngineNotAvailableException(
        string engineName,
        string resolution,
        string message,
        Func<ActionContext, ValueTask<bool>>? action = null) : base(message)
    {
        EngineName = engineName;
        Resolution = resolution;
        Action = action ?? (_ =>
        {
            Clipboard.SetDataObject(this.ToString());
            return ValueTask.FromResult(true);
        });
    }

    public EngineNotAvailableException(
        string engineName,
        string resolution,
        string message,
        Exception innerException) : base(message, innerException)
    {
        EngineName = engineName;
        Resolution = resolution;
    }
    
    public EngineNotAvailableException(
    string engineName,
    string resolution,
    string message,
    string errorIconPath,
    Func<ActionContext, ValueTask<bool>>? action = null) : base(message)
    {
        EngineName = engineName;
        Resolution = resolution;
        ErrorIcon = errorIconPath;
        Action = action ?? (_ =>
        {
            Clipboard.SetDataObject(this.ToString());
            return ValueTask.FromResult(true);
        });
    }

    public override string ToString()
    {
        return $"Engine {EngineName} is not available.\n Try to {Resolution}\n {base.ToString()}";
    }
}
