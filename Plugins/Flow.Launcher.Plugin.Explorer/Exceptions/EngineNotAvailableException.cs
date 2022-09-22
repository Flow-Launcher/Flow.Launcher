using System;
using Flow.Launcher.Plugin.Explorer.Search.IProvider;

namespace Flow.Launcher.Plugin.Explorer.Exceptions;

public class EngineNotAvailableException : Exception
{
    public string EngineName { get; }
    public string Resolution { get; }
    public EngineNotAvailableException(string engineName,
        string resolution,
        string message) : base(message)
    {
        EngineName = engineName;
        Resolution = resolution;
    }
    
    public EngineNotAvailableException(string engineName,
        string resolution,
        string message,
        Exception innerException) : base(message, innerException)
    {
        EngineName = engineName;
        Resolution = resolution;
    }

    public override string ToString()
    {
        return $"Engine {EngineName} is not available.\n Try to {Resolution}\n {base.ToString()}";
    }
}
