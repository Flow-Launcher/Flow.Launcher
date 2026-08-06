# Repository Atlas: Flow.Launcher

## Project Responsibility
Flow.Launcher is a Windows productivity launcher (similar to Alfred/Raycast) built with WPF and Avalonia. It provides fast, extensible search capabilities through a plugin-based architecture, allowing users to launch applications, search files, perform calculations, and execute custom commands via a global hotkey-activated search window.

## System Entry Points
- **`Flow.Launcher/`**: WPF main application entry point (legacy, stable)
- **`Flow.Launcher.Avalonia/`**: Avalonia UI migration entry point (active development ~35-40%)
- **`Flow.Launcher.sln`**: Solution file for building all projects
- **`Scripts/post_build.ps1`**: Packaging script for releases

## Technology Stack
- **.NET 9.0** targeting `net9.0-windows10.0.19041.0`
- **WPF** (original UI) - `Flow.Launcher/`
- **Avalonia** (migration in progress) - `Flow.Launcher.Avalonia/`
- **CommunityToolkit.Mvvm** for MVVM patterns and DI
- **FluentAvalonia** for modern UI in Avalonia version
- **Squirrel.Windows** for application updates
- **NLog** for logging

## Repository Directory Map

| Directory | Responsibility Summary | Detailed Map |
|-----------|------------------------|--------------|
| `Flow.Launcher/` | **WPF Main Application**: Host process orchestrating UI layer, plugin lifecycle, and system integrations (tray, hotkeys). Uses MVVM, DI, and Bridge patterns. | [View Map](Flow.Launcher/codemap.md) |
| `Flow.Launcher.Avalonia/` | **Avalonia UI Migration**: Modern cross-platform UI implementation with WPF compatibility shim for plugin support. Active development target. | [View Map](Flow.Launcher.Avalonia/codemap.md) |
| `Flow.Launcher.Core/` | **Business Logic Engine**: Plugin lifecycle management, internationalization, application updates via Squirrel, and community plugin store integration. | [View Map](Flow.Launcher.Core/codemap.md) |
| `Flow.Launcher.Infrastructure/` | **Shared Services Layer**: Fuzzy search (StringMatcher), persistence (JSON storage), global hotkeys, logging, Win32 API wrappers, and Settings model. | [View Map](Flow.Launcher.Infrastructure/codemap.md) |
| `Flow.Launcher.Plugin/` | **Plugin SDK**: Interfaces and models for plugin development (`IPlugin`, `IAsyncPlugin`, `Result`, `Query`, `IPublicAPI`). The formal contract between core and extensions. | [View Map](Flow.Launcher.Plugin/codemap.md) |
| `Plugins/` | **Built-in Plugins**: 12 official plugins (Calculator, Explorer, Program, WebSearch, Shell, etc.) providing core functionality. | [View Map](Plugins/codemap.md) |
| `Flow.Launcher.Test/` | **Unit Tests**: NUnit-based test suite for the application. |

## Architecture Overview

### Data Flow
```
User Input → MainWindow → MainViewModel → PluginManager → Plugins → Results → UI
```

### Key Architectural Patterns
1. **MVVM**: Strict separation of UI and logic across both WPF and Avalonia implementations
2. **Dependency Injection**: `CommunityToolkit.Mvvm.Ioc` for service management
3. **Plugin Architecture**: Interface-based extensibility via `IPlugin`/`IAsyncPlugin`
4. **Bridge Pattern**: `PublicAPIInstance`/`AvaloniaPublicAPI` decouples plugins from internal implementation
5. **Static Coordinator**: `PluginManager` acts as central hub for plugin operations

### Migration Status (WPF → Avalonia)
- **WPF**: Stable, feature-complete, maintenance mode
- **Avalonia**: ~35-40% migrated, active development, includes WPF compatibility shim for plugins
- **Shared**: Core, Infrastructure, and Plugin SDK are framework-agnostic

## Build & Run

```bash
# Build entire solution
dotnet build

# Run WPF version
./Output/Debug/Flow.Launcher.exe

# Run Avalonia version
./Output/Debug/Avalonia/Flow.Launcher.Avalonia.exe
```

## Key Documentation
- `AGENTS.md`: Essential information for AI agents working on this codebase
- `AVALONIA_MIGRATION_CHECKLIST.md`: Detailed migration progress tracking
- `Flow.Launcher.Plugin/README.md`: Plugin SDK documentation
- `.editorconfig`: Code style rules
- `Settings.XamlStyler`: XAML formatting rules
