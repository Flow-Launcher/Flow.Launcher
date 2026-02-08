# AGENTS.md - Flow.Launcher

Windows productivity launcher (like Alfred/Raycast) with dual UI frameworks:
- **WPF**: `Flow.Launcher/` (original)
- **Avalonia**: `Flow.Launcher.Avalonia/` (migration ~35-40%)
- **.NET 9.0** targeting `net9.0-windows10.0.19041.0`
- **CommunityToolkit.Mvvm** for MVVM

See `AVALONIA_MIGRATION_CHECKLIST.md` for migration progress.

## Commands

```bash
# Build
dotnet build
dotnet build -c Release
nuget restore

# Test (NUnit 4.x)
dotnet test
dotnet test --filter "FullyQualifiedName~FuzzyMatcherTest"
dotnet test --filter "Name~WhenSearching"

# Run
./Output/Debug/Flow.Launcher.exe              # WPF
./Output/Debug/Avalonia/Flow.Launcher.Avalonia.exe  # Avalonia
```

## Code Style

### Naming
- **PascalCase**: types, public members, methods, properties
- **camelCase**: locals, parameters
- **_camelCase**: private fields (underscore prefix)
- **PascalCase**: constants (per `.editorconfig`)
- No `this.` qualifier

### C# Conventions
```csharp
// File-scoped namespaces
namespace Flow.Launcher.ViewModel;

// Prefer var when type is apparent
var results = new List<Result>();

// Allman braces, always required
if (condition)
{
    DoSomething();
}

// 4-space indent (code), 2-space (XML/XAML)
```

### Imports
- Sort system directives first (`dotnet_sort_system_directives_first = true`)
- Using placement: outside namespace

### Error Handling
- Use nullable reference types (enabled in Avalonia project)
- Prefer `is null` checks over reference equality
- Use null propagation and coalesce expressions

### XAML (XamlStyler)
- One attribute per line (except ≤2)
- Space before closing slash: `<Element />`
- Attribute order: `x:Class` → `xmlns` → `x:Key/Name` → layout → size → margin/padding → others

**WPF**: `.xaml` with `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"`
**Avalonia**: `.axaml` with `xmlns="https://github.com/avaloniaui"`

## MVVM Patterns

```csharp
// Avalonia: CommunityToolkit.Mvvm
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _queryText = string.Empty;
    
    [RelayCommand]
    private void Search() { }
}

// WPF: BaseModel with manual INPC
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

## Plugin Architecture

```csharp
public interface IAsyncPlugin
{
    Task<List<Result>> QueryAsync(Query query, CancellationToken token);
    Task InitAsync(PluginInitContext context);
}

// Result creation
new Result
{
    Title = "Title",
    SubTitle = "Subtitle",
    IcoPath = "Images/icon.png",
    Score = 100,
    Action = context => true  // return true to hide window
};
```

## Avalonia Migration Notes

| WPF | Avalonia |
|-----|----------|
| `.xaml` | `.axaml` |
| `Visibility` | `IsVisible` |
| `Collapsed` | Not available |
| `BoolToVisibilityConverter` | Direct bool binding |
| `xmlns:microsoft` | `xmlns:avaloniaui` |

Use `#if AVALONIA` for conditional compilation.

## Testing

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
}
```

## Key Files

| File | Purpose |
|------|---------|
| `MainViewModel.cs` | Search window logic |
| `ResultsViewModel.cs` | Results management |
| `Settings.cs` | User settings |
| `PluginManager.cs` | Plugin lifecycle |
| `StringMatcher.cs` | Fuzzy search |
| `IPublicAPI.cs` | Plugin API |

## Gotchas

1. Build order matters - plugins must build before WPF
2. Build kills running `Flow.Launcher.exe` automatically
3. Plugins run in separate app domains
4. Settings auto-save via Fody PropertyChanged
5. Some tests require Windows Search service (`WSearch`)
6. Avalonia has `<Nullable>enable</Nullable>`, WPF does not

## Resources

- `.editorconfig` - C#/VB style rules
- `Settings.XamlStyler` - XAML formatting
- `Flow.Launcher.Plugin/README.md` - Plugin SDK docs
