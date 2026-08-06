# Flow.Launcher.Core

Core business logic engine responsible for plugin orchestration, application updates, and resource management.

## Responsibility
- **Plugin Lifecycle Management**: Discovers, loads, initializes, queries, and disposes of plugins. Supports C#, Python, Node.js, and executable plugins.
- **Resource & Internationalization**: Coordinates multi-language support and theme resources, dynamically merging XAML dictionaries.
- **Application Updates**: Manages the self-update mechanism using Squirrel.Windows and GitHub releases.
- **External Plugin Integration**: Interfaces with the community plugin store and manifest systems to allow discovery and installation of community-made plugins.

## Design
- **Centralized Coordinator (Static Hub)**: `PluginManager` is the primary static entry point for plugin operations, maintaining global lists of loaded plugins.
- **Dependency Injection**: Utilizes `CommunityToolkit.Mvvm.DependencyInjection` (Ioc) to access services like `IPublicAPI`.
- **Interface-Based Extensibility**: Uses interfaces like `IPlugin`, `IAsyncPlugin`, and `IPluginI18n` to decouple core logic from specific plugin implementations.
- **Asynchronous Execution**: Comprehensive use of `async/await` and `CancellationToken` for non-blocking plugin queries and lifecycle events.
- **Resource Merging Pattern**: `Internationalization` dynamically merges `ResourceDictionary` objects from both the main application and individual plugins into the global application resources.

## Flow
### Plugin Query Flow
1. **Parsing**: raw user input is parsed into a structured `Query` object (via `QueryBuilder`).
2. **Selection**: `PluginManager.ValidPluginsForQuery` identifies matching plugins based on action keywords or global status.
3. **Execution**: `PluginManager.QueryForPluginAsync` executes queries in parallel across selected plugins.
4. **Aggregation**: `Result` objects are aggregated, updated with metadata (PluginID, directory), and returned to the UI layer.

### Update Flow
1. `Updater.UpdateAppAsync` checks the GitHub repository for new releases via Squirrel.
2. If found, updates are downloaded and applied.
3. Portable data is migrated to the new version directory if a portable install is detected.

## Integration
- **`Flow.Launcher.Infrastructure`**: Consumes settings, constants, and logging utilities.
- **`Flow.Launcher.Plugin`**: Implements the SDK interfaces used by plugins to communicate with the host.
- **`Squirrel.Windows`**: External dependency for installation and delta-update logic.
- **`Avalonia`/`WPF`**: The `Internationalization` system interacts directly with the UI framework's resource dictionaries.
