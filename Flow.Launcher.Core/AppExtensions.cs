using System.Windows;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core;

/// <summary>
/// Extension properties and functions of the current application singleton object.
/// </summary>
public static class AppExtensions
{
    /// <summary>
    /// Gets the public API of the current application singleton object.
    /// </summary>
    public static IPublicAPI API => (Application.Current as IApp)!.PublicAPI;
}
