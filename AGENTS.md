# AGENTS.md - Flow.Launcher

This document provides essential information for AI agents working on the Flow.Launcher codebase.

## Project Overview

Flow.Launcher is a Windows productivity launcher (similar to Alfred/Raycast) built with:
- **WPF** (original UI framework) - `Flow.Launcher/`
- **Avalonia** (migration in progress ~35-40%) - `Flow.Launcher.Avalonia/`
- **.NET 9.0** targeting `net9.0-windows10.0.19041.0`
- **CommunityToolkit.Mvvm** for MVVM patterns
- **FluentAvalonia** for modern UI in Avalonia version

The codebase is actively being migrated from WPF to Avalonia. See `AVALONIA_MIGRATION_CHECKLIST.md` for detailed progress.

---

## Essential Commands

### Build

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build Flow.Launcher.Avalonia/Flow.Launcher.Avalonia.csproj

# Release build
dotnet build -c Release

# Restore dependencies (required for CI)
nuget restore
```

### Test

```bash
# Run all tests
dotnet test

# Run tests with verbosity
dotnet test --verbosity normal

# Run specific test file/class
dotnet test --filter "ClassName=FuzzyMatcherTest"
```

### Run

```bash
# Run WPF version
./Output/Debug/Flow.Launcher.exe

# Run Avalonia version
./Output/Debug/Avalonia/Flow.Launcher.Avalonia.exe
```

### Output Locations

| Configuration | WPF Output | Avalonia Output |
|---------------|------------|-----------------|
| Debug | `Output/Debug/` | `Output/Debug/Avalonia/` |
| Release | `Output/Release/` | `Output/Release/Avalonia/` |

---

## Project Structure

```
Flow.Launcher/
├── Flow.Launcher/              # WPF main application
│   ├── MainWindow.xaml         # Main search window
│   ├── SettingWindow.xaml      # Settings window
│   ├── ViewModel/              # ViewModels (MVVM)
│   ├── SettingPages/           # Settings page views/viewmodels
│   ├── Helper/                 # Utility classes
│   ├── Converters/             # XAML value converters
│   ├── Themes/                 # Theme XAML files
│   ├── Languages/              # Localization (*.xaml)
│   └── Resources/              # Icons, fonts, styles
│
├── Flow.Launcher.Avalonia/     # Avalonia main application (migration)
│   ├── MainWindow.axaml        # Main search window
│   ├── Views/                  # Avalonia views
│   │   ├── SettingPages/       # Settings pages
│   │   └── Controls/           # Custom controls
│   ├── ViewModel/              # ViewModels
│   ├── Helper/                 # Utilities
│   ├── Converters/             # Avalonia converters
│   └── Themes/                 # Avalonia themes
│
├── Flow.Launcher.Plugin/       # Plugin SDK (shared)
│   ├── Interfaces/             # IPlugin, IPublicAPI, etc.
│   ├── Result.cs               # Search result model
│   ├── Query.cs                # Query model
│   └── PluginMetadata.cs       # Plugin metadata
│
├── Flow.Launcher.Infrastructure/  # Shared infrastructure
│   ├── UserSettings/           # Settings models
│   ├── StringMatcher.cs        # Fuzzy search algorithm
│   └── Logger/                 # Logging utilities
│
├── Flow.Launcher.Core/         # Core business logic
│   ├── Plugin/                 # Plugin management
│   │   └── PluginManager.cs    # Plugin lifecycle
│   ├── Resource/               # Internationalization
│   └── ExternalPlugins/        # Plugin store
│
├── Plugins/                    # Built-in plugins
│   ├── Flow.Launcher.Plugin.Calculator/
│   ├── Flow.Launcher.Plugin.Explorer/
│   ├── Flow.Launcher.Plugin.Program/
│   ├── Flow.Launcher.Plugin.WebSearch/
│   └── ... (10+ plugins)
│
├── Flow.Launcher.Test/         # Unit tests (NUnit)
│
└── Scripts/                    # Build scripts
    └── post_build.ps1          # Packaging script
```

---

## Code Conventions

### Naming

- **PascalCase** for public members, types, properties, methods
- **camelCase** for local variables, parameters
- **_camelCase** for private fields (underscore prefix)
- **UPPER_CASE** constants are PascalCase per `.editorconfig`
- No `this.` qualifier (per `.editorconfig`)

### C# Style

```csharp
// File-scoped namespaces preferred
namespace Flow.Launcher.ViewModel;

// Prefer var when type is apparent
var results = new List<Result>();

// Braces always required
if (condition)
{
    DoSomething();
}

// Allman brace style (new line before opening brace)
public void Method()
{
    // ...
}

// 4-space indentation for code
// 2-space indentation for XML/XAML
```

### MVVM Patterns

The project uses **CommunityToolkit.Mvvm** with source generators:

```csharp
// ViewModels use ObservableObject base
public partial class MainViewModel : ObservableObject
{
    // [ObservableProperty] generates property + change notification
    [ObservableProperty]
    private string _queryText = string.Empty;
    
    // [RelayCommand] generates ICommand implementation
    [RelayCommand]
    private void Search() { ... }
}

// WPF uses BaseModel which wraps INotifyPropertyChanged
public class MainViewModel : BaseModel
{
    public string QueryText
    {
        get => _queryText;
        set
        {
            if (_queryText != value)
            {
                _queryText = value;
                OnPropertyChanged();
            }
        }
    }
}
```

### XAML Style

Uses XamlStyler with specific rules (see `Settings.XamlStyler`):
- One attribute per line (except when ≤2)
- Specific attribute ordering (x:Class first, then xmlns, etc.)
- Space before closing slash: `<Element />`

**WPF XAML** (`.xaml`):
```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
```

**Avalonia AXAML** (`.axaml`):
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="using:FluentAvalonia.UI.Controls">
```

---

## Plugin Architecture

### Plugin Interface

All plugins implement `IPlugin` or `IAsyncPlugin`:

```csharp
public interface IPlugin : IAsyncPlugin
{
    List<Result> Query(Query query);
    void Init(PluginInitContext context);
}

public interface IAsyncPlugin
{
    Task<List<Result>> QueryAsync(Query query, CancellationToken token);
    Task InitAsync(PluginInitContext context);
}
```

### Creating Results

```csharp
return new List<Result>
{
    new Result
    {
        Title = "Result Title",
        SubTitle = "Optional subtitle",
        IcoPath = "Images/icon.png",  // Relative to plugin directory
        Score = 100,                   // Higher = better match
        Action = context =>
        {
            // Execute action, return true to hide window
            return true;
        }
    }
};
```

### Plugin Metadata

Each plugin needs `plugin.json`:
```json
{
    "ID": "unique-guid",
    "ActionKeyword": "keyword",
    "Name": "Plugin Name",
    "Description": "Description",
    "Author": "Author",
    "Version": "1.0.0",
    "Language": "csharp",
    "Website": "https://...",
    "IcoPath": "Images\\icon.png",
    "ExecuteFileName": "Plugin.dll"
}
```

### Plugin Settings

```csharp
// Load settings
var settings = context.API.LoadSettingJsonStorage<MySettings>();

// Save (automatic on app close, or manual)
context.API.SaveSettingJsonStorage<MySettings>();
```

---

## Key Classes & Files

| Class/File | Purpose |
|------------|---------|
| `MainViewModel.cs` | Main search window logic |
| `ResultsViewModel.cs` | Search results management |
| `Settings.cs` | User settings model |
| `PluginManager.cs` | Plugin lifecycle management |
| `StringMatcher.cs` | Fuzzy search algorithm |
| `IPublicAPI.cs` | Plugin API interface |
| `Result.cs` | Search result model |
| `Query.cs` | Search query model |

---

## Testing

- **Framework**: NUnit 4.x
- **Mocking**: Moq
- **Test file naming**: `*Test.cs`
- **Test class attribute**: `[TestFixture]`

```csharp
[TestFixture]
public class FuzzyMatcherTest
{
    [Test]
    public void WhenSearching_ThenReturnsExpectedResults()
    {
        var matcher = new StringMatcher(null);
        var result = matcher.FuzzyMatch("chr", "Chrome");
        ClassicAssert.IsTrue(result.RawScore > 0);
    }
    
    [TestCase("chrome")]
    [TestCase("chr")]
    public void ParameterizedTest(string query)
    {
        // ...
    }
}
```

---

## Avalonia Migration Notes

When working on the Avalonia migration:

1. **File extensions**: Use `.axaml` not `.xaml` for Avalonia
2. **Namespace**: Use `xmlns="https://github.com/avaloniaui"`
3. **Controls**: Use FluentAvalonia `ui:SettingsExpander` for settings pages
4. **Visibility**: Use `IsVisible` not `Visibility` (no `Collapsed` enum)
5. **Converters**: Different converter approach (see `Converters/`)
6. **Define directive**: `#if AVALONIA` for conditional compilation

**Key differences**:
```xml
<!-- WPF -->
<Button Visibility="{Binding IsVisible, Converter={StaticResource BoolToVisibility}}" />

<!-- Avalonia -->
<Button IsVisible="{Binding IsVisible}" />
```

---

## Localization

Resources are in `Languages/*.xaml` files:

```xml
<!-- Languages/en.xaml -->
<sys:String x:Key="startFlowLauncherOnSystemStartup">Start Flow Launcher on system startup</sys:String>
```

Usage in XAML:
```xml
<!-- WPF -->
<TextBlock Text="{DynamicResource startFlowLauncherOnSystemStartup}" />

<!-- Avalonia (custom extension) -->
<TextBlock Text="{i18n:Localize startFlowLauncherOnSystemStartup}" />
```

---

## Common Tasks

### Adding a new setting

1. Add property to `Flow.Launcher.Infrastructure/UserSettings/Settings.cs`
2. Add UI in appropriate settings page (WPF and/or Avalonia)
3. Bind to ViewModel property

### Adding a new plugin

1. Create project under `Plugins/` folder
2. Reference `Flow.Launcher.Plugin`
3. Implement `IPlugin` or `IAsyncPlugin`
4. Add `plugin.json` metadata
5. Add to solution and build dependencies in WPF project

### Fixing a ViewModel binding

1. Ensure property raises `PropertyChanged` or uses `[ObservableProperty]`
2. Check DataContext is set correctly
3. Verify binding path matches property name exactly

---

## Gotchas & Tips

1. **Build order matters**: WPF project depends on plugins being built first
2. **Kill running instance**: Build kills running `Flow.Launcher.exe` automatically
3. **Plugin isolation**: Plugins run in separate app domains, can't share state directly
4. **Settings persist**: Changes to `Settings.cs` properties auto-save via Fody PropertyChanged
5. **Windows Search**: Some tests require Windows Search service (`WSearch`) to be running
6. **Nullable**: Avalonia project has `<Nullable>enable</Nullable>`, WPF does not
7. **Framework reference**: Avalonia still references WPF assemblies for `IPublicAPI` compatibility

---

## CI/CD

GitHub Actions workflow (`.github/workflows/dotnet.yml`):
- Runs on Windows
- Uses .NET 9.0
- Builds Release configuration
- Runs tests
- Creates installer via Squirrel

Key environment variables:
- `FlowVersion`: Version number (e.g., "1.20.2")
- `BUILD_NUMBER`: CI build number

---

## Resources

- **Migration checklist**: `AVALONIA_MIGRATION_CHECKLIST.md`
- **Plugin SDK docs**: `Flow.Launcher.Plugin/README.md`
- **EditorConfig**: `.editorconfig` for code style
- **XAML formatting**: `Settings.XamlStyler`
