using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core
{
    /// <summary>
    /// Interface for the current application singleton object exposing the properties
    /// and functions that can be accessed from anywhere in the application.
    /// </summary>
    public interface IApp
    {
        public IPublicAPI PublicAPI { get; }
    }
}
