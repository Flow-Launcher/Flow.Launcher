using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin;

/// <summary>
/// Sent through <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default"/> on the UI thread
/// once a plugin has been removed from every registry and before its assembly load context is unloaded.
/// Recipients must drop everything they hold from the plugin (results, setting panels), otherwise the
/// load context stays alive and the plugin directory stays locked until restart.
/// </summary>
public sealed record PluginUnloadingMessage(PluginMetadata Metadata);

/// <summary>
/// Sent through <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default"/> on the UI thread
/// after a plugin has been loaded and initialized at runtime by a hot reload, install or update.
/// </summary>
public sealed record PluginLoadedMessage(PluginMetadata Metadata);
