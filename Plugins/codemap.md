# Plugins Codemap

This directory contains the built-in plugins for Flow.Launcher. These plugins provide core functionality and are maintained as part of the main repository.

## Responsibility
The `Plugins/` directory houses the source code for all official, built-in plugins. Each plugin is a separate project that implements the Flow.Launcher plugin SDK to extend the launcher's capabilities. These plugins are compiled as DLLs and loaded by the `PluginManager` at runtime.

## Plugin Architecture
Built-in plugins follow a consistent structure and lifecycle:
- **Entry Point**: A `Main.cs` file that implements `IPlugin` (synchronous) or `IAsyncPlugin` (asynchronous).
- **Metadata**: A `plugin.json` file defining the plugin's unique ID, name, version, and action keywords.
- **Settings**: A `Settings.cs` class for persisting user preferences, typically managed via `ISettingProvider` and loaded/saved using the `IPublicAPI`.
- **Localization**: A `Languages/` folder containing XAML files for internationalization (`IPluginI18n`).
- **Assets**: An `Images/` folder for the plugin icon and other graphical assets.
- **UI**: `Views/` and `ViewModels/` folders for the plugin's settings interface, supporting both WPF and Avalonia.

## Key Plugins

| Plugin | Description |
| :--- | :--- |
| **Calculator** | Performs mathematical calculations, including hex values and advanced functions. |
| **Explorer** | Finds and manages files and folders via Windows Search or Everything. |
| **Program** | Searches and launches installed programs (Win32 and UWP). |
| **WebSearch** | Provides web search capabilities with customizable search engines. |
| **Shell** | Executes shell/terminal commands directly from the launcher. |
| **BrowserBookmark** | Searches and opens bookmarks from various web browsers. |
| **ProcessKiller** | Allows users to find and kill running processes. |
| **WindowsSettings** | Searches for settings inside the Windows Control Panel and Settings app. |
| **PluginsManager** | Installs, uninstalls, and updates Flow Launcher plugins from the search window. |
| **PluginIndicator** | Provides suggestions for plugin action keywords. |
| **Sys** | Provides system-related commands (e.g., shutdown, restart, lock). |
| **Url** | Directly opens typed URLs in the default browser. |

## Common Patterns
- **Interface-Driven**: Plugins leverage interfaces like `IContextMenu`, `ISettingProvider`, and `IAsyncReloadable` to integrate deeply with the launcher's UI and lifecycle.
- **Contextual Initialization**: The `Init` method receives a `PluginInitContext`, providing plugins with access to the `IPublicAPI` for tasks like showing messages, changing queries, or loading settings.
- **Lazy Loading/Caching**: Plugins like `Program` and `Explorer` use caching mechanisms and background indexing to ensure search results are returned instantly.
- **Shared Infrastructure**: Plugins often depend on `Flow.Launcher.Infrastructure` for logging, settings management, and fuzzy matching logic.
- **Internationalization**: Use of `DynamicResource` (WPF) and `Localize` (Avalonia) extensions with the `Languages/` resource files for multi-language support.
