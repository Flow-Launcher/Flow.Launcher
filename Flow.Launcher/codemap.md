# Flow.Launcher (WPF Main Application)

## Responsibility
This directory contains the entry point and the primary WPF user interface for Flow Launcher. It acts as the host for the entire application, orchestrating the lifecycle, plugin management integration, and user interaction. 

Its main duties include:
- **Application Lifecycle**: Managing startup, single-instance enforcement, and graceful shutdown.
- **Main Search UI**: Providing the global search window (`MainWindow`) and handling its positioning, animations, and visibility.
- **Settings Management**: Hosting the multi-page settings window and ensuring user configurations are persisted.
- **Plugin API Hosting**: Implementing the `IPublicAPI` which serves as the primary bridge between the application core and external plugins.
- **System Integration**: Handling system tray icons, global hotkeys, and Windows-native notifications.

## Design Patterns
- **MVVM (Model-View-ViewModel)**: Strict separation of UI (`.xaml`/`.cs`) and logic (`MainViewModel`, `ResultsViewModel` in `Flow.Launcher.ViewModel`).
- **Dependency Injection (DI)**: Uses `Microsoft.Extensions.DependencyInjection` and `CommunityToolkit.Mvvm` to manage service lifetimes (e.g., `Settings`, `MainViewModel`, `IPublicAPI` are singletons).
- **Bridge Pattern**: `PublicAPIInstance` acts as a bridge, decoupling the Plugin SDK (`Flow.Launcher.Plugin`) from the internal implementation details of the core application.
- **Observer Pattern**: Extensive use of `INotifyPropertyChanged` and event handlers to sync UI state with settings and search results.
- **Singleton**: Ensured via the DI container for core application-wide state.

## Key Classes
- **`App.xaml.cs`**: The application's orchestrator. It configures DI, initializes the `PluginManager`, handles unhandled exceptions, and manages the single-instance logic.
- **`MainWindow.xaml.cs`**: The main search interface. Handles complex UI logic like blur effects (Backdrop), custom window positioning on multiple monitors, and search-box focus management.
- **`PublicAPIInstance.cs`**: The concrete implementation of the `IPublicAPI` interface. This is the object passed to every plugin, allowing them to interact with the launcher (e.g., `ChangeQuery`, `ShowMsg`, `OpenUrl`).
- **`SettingWindow.xaml.cs`**: The UI for user configuration. It uses a `NavigationView` to switch between different settings categories (General, Plugins, Theme, etc.).
- **`Notification.cs`**: A wrapper for the Windows notification system (using `Microsoft.Toolkit.Uwp.Notifications`), falling back to custom WPF windows (`Msg.xaml`) for older OS versions.
- **`ResultListBox.xaml.cs`**: A customized `ListBox` that handles specialized interaction logic like drag-and-drop of file results and mouse-over selection behaviors.

## Data & Control Flow
1. **Startup Flow**: 
   `App.Main()` -> Load `Settings` -> `App.OnStartup()` -> `PluginManager.LoadPlugins()` -> `PluginManager.InitializePluginsAsync()` -> Create `MainWindow`.
2. **Search Flow**:
   User types in `MainWindow.QueryTextBox` -> `MainViewModel.QueryText` updates -> `MainViewModel.QueryResults()` -> `PluginManager.QueryAsync()` -> Results update in `ResultsViewModel` -> `MainWindow` UI reflects results.
3. **Execution Flow**:
   User selects result -> `MainViewModel.SelectResult()` -> Result's `Action` (defined in plugin) is executed via `MainViewModel`.
4. **Settings Flow**:
   User changes setting in `SettingWindow` -> `Settings` property updates (via binding) -> `Settings.Save()` is called (auto-persisted to JSON) -> UI/Core responds to `PropertyChanged`.

## Integration Points
- **`Flow.Launcher.Core`**: Heavily dependent on `PluginManager` for all search logic and `Internationalization` for multi-language support.
- **`Flow.Launcher.Infrastructure`**: Consumes `Settings`, `Logger`, `Http`, and `ImageLoader`.
- **`Flow.Launcher.ViewModel`**: The primary data source for all windows in this project.
- **`Flow.Launcher.Plugin`**: Provides the SDK interfaces and models used to communicate with plugins.
- **External Libraries**: 
    - `ModernWpf`: For Windows 11-style UI controls.
    - `CommunityToolkit.Mvvm`: For the MVVM framework and DI.
    - `Squirrel`: For application updates and restart logic.
