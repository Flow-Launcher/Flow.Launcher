# Flow.Launcher.Plugin Codemap

## Responsibility
The `Flow.Launcher.Plugin` module serves as the **Plugin SDK** for Flow.Launcher. It defines the essential building blocks, interfaces, and models required for developers to create plugins. It acts as the formal contract between the Flow.Launcher core application and its extensibility layer, ensuring a stable and consistent API for search results, UI interaction, and system integration across different plugin languages (C#, Python, JavaScript, etc.).

## Design
The SDK is built on several key architectural patterns:
- **Interface-based Extensibility**: The core functionality is driven by interfaces (`IPlugin`, `IAsyncPlugin`) that define how plugins are queried. Optional features are exposed via specialized interfaces like `IContextMenu`, `ISettingProvider`, and `IReloadable`.
- **Command/Action Pattern**: The `Result` class encapsulates its own execution logic through `Action` and `AsyncAction` delegates. This decouples the search engine from the specific logic of what happens when a result is clicked.
- **Contextual Initialization**: Plugins receive their environment data and a reference to the application's capabilities via `PluginInitContext` and the `IPublicAPI` interface during the `Init` phase.
- **Marker Interfaces**: The `IFeatures` interface serves as a base for all optional capability interfaces, allowing the core system to dynamically discover and invoke plugin features.

### Key Components:
- **`Result`**: The primary model for search results, containing display data (Title, SubTitle, Icons) and execution delegates.
- **`Query`**: Represents the user's input, providing parsed search terms and action keywords.
- **`IPublicAPI`**: The interface providing plugins with access to global functions (logging, UI control, settings storage, HTTP utilities).
- **`PluginMetadata`**: Identity and configuration data extracted from the plugin's `plugin.json` manifest.

## Flow
The lifecycle and data flow of a plugin within Flow.Launcher follow a strictly defined sequence:

1.  **Discovery & Load**: The `PluginManager` (in Core) scans the `Plugins` directory, parses `plugin.json` into `PluginMetadata`, and instantiates the plugin class.
2.  **Initialization**: The `Init(PluginInitContext)` method is called. The plugin stores the `IPublicAPI` reference for future use.
3.  **Query Loop**: When the user types, the system constructs a `Query` object. It then calls `Query(Query)` (Sync) or `QueryAsync(Query, CancellationToken)` (Async) on all relevant plugins.
4.  **Result Aggregation**: Plugins return a `List<Result>`. The Core sorts these results based on `Score` and displays them in the UI.
5.  **Action Execution**: When a result is selected, the Core invokes `Result.ExecuteAsync()`, which runs the plugin-defined delegate.
6.  **Feedback**: Plugins use `IPublicAPI` to trigger side-effects, such as changing the current query, showing notifications, or saving persistent settings.

## Integration
- **Search Integration**: Plugins hook into the main launcher window by providing results that match user queries.
- **UI Integration**: Plugins provide custom settings panels via `ISettingProvider` and additional context menu options via `IContextMenu`.
- **Storage Integration**: Plugins persist their state using `LoadSettingJsonStorage<T>` (for configuration) or `LoadCacheBinaryStorageAsync<T>` (for high-performance data caching) provided by the API.
- **System Integration**: Through `IPublicAPI`, plugins can execute shell commands, manage the clipboard, download files, and subscribe to system-wide events like theme changes or keyboard hooks.
