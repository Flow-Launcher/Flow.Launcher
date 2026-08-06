# Flow.Launcher.Avalonia/

## Responsibility
The `Flow.Launcher.Avalonia` directory is the core of the Avalonia-based UI implementation for Flow Launcher. It handles the application lifecycle, the primary search interface, and provides compatibility layers for existing plugins that were originally designed for the WPF version. It serves as the main entry point for the Avalonia flavor of the application.

## Design
- **MVVM (Model-View-ViewModel)**: Separation of UI (`MainWindow.axaml`) and logic (`MainViewModel.cs`).
- **Dependency Injection (DI)**: Uses `Microsoft.Extensions.DependencyInjection` and `CommunityToolkit.Mvvm`'s `Ioc` for service management.
- **Bridge Pattern**: `AvaloniaPublicAPI` adapts the `IPublicAPI` interface to work with Avalonia's infrastructure, allowing plugins to function without major modifications.
- **Singleton**: Core services like `Settings`, `MainViewModel`, and `Internationalization` are registered as singletons within the DI container.
- **WPF Compatibility Shim**: A unique architectural decision to run a background WPF `Application` instance to support legacy plugins that rely on `System.Windows.Application.Current`.

### Key Classes

| Class | Purpose |
|-------|---------|
| `Program.cs` | Entry point. Initializes a background WPF `Application` instance for plugin compatibility before starting the Avalonia app. |
| `App.axaml.cs` | Manages application lifecycle, DI container configuration, settings loading, and plugin initialization. |
| `MainWindow.axaml.cs` | Code-behind for the primary search window. Handles window positioning, focus management, and keyboard shortcuts (Escape, Arrows). |
| `AvaloniaPublicAPI.cs` | Implementation of `IPublicAPI`. Provides plugins with access to Flow Launcher's core functionality (Querying, UI control, Clipboard, etc.) in the Avalonia context. |

## Flow

### Application Startup
1. **`Program.Main`**: Initializes `System.Windows.Application` (WPF) to provide `Application.Current.Resources` for legacy plugins.
2. **`App.Initialize`**: 
    - Loads user settings from JSON via `FlowLauncherJsonStorage`.
    - Configures the DI container (`ConfigureDI`).
    - Injects translations into application resources.
3. **`App.OnFrameworkInitializationCompleted`**:
    - Resolves `MainViewModel` from DI.
    - Instantiates `MainWindow`.
    - Starts asynchronous plugin initialization (`InitializePluginsAsync`).

### Search Execution
1. User input in `MainWindow`'s `QueryTextBox` updates `MainViewModel.QueryText` via binding.
2. `MainViewModel` (in `Flow.Launcher.Avalonia/ViewModel`) triggers `PluginManager` to query active plugins.
3. Results are returned and displayed in the UI.

## Integration
- **`Flow.Launcher.Core`**: Directly integrates with `PluginManager` for plugin lifecycle and `Internationalization` for multi-language support.
- **`Flow.Launcher.Infrastructure`**: Relies on `UserSettings`, `Logger`, and `StringMatcher` (fuzzy search logic).
- **`Flow.Launcher.Plugin`**: Implements the `IPublicAPI` interface to allow plugins to interact with the Avalonia-hosted app.
- **WPF Ecosystem**: Maintains a runtime dependency on WPF (`System.Windows`) to ensure that plugins using WPF-specific resources or `Application.Current` do not crash.

## Avalonia vs WPF Implementation Details
- **WPF Shim**: Unlike the original WPF version, the Avalonia version must manually spin up a WPF environment in `Program.cs` to maintain plugin compatibility.
- **Window Positioning**: `MainWindow` uses Avalonia's `Screens` API to center itself at 25% from the top of the primary screen, mimicking the WPF behavior.
- **Event Handling**: Uses Avalonia-specific events like `Deactivated` and `PointerPressed` for window management and dragging.
- **DI Strategy**: Uses a modern `ServiceCollection`-based DI approach compared to the older WPF implementation's service resolution.
