# Flow.Launcher.Avalonia KNOWLEDGE BASE

Avalonia host for the migrated app. It is not just a UI port: it carries explicit WPF compatibility shims and partial feature parity.

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| Entry point | `Program.cs` | starts Avalonia and boots WPF `Application` shim |
| App startup | `App.axaml`, `App.axaml.cs` | DI, resources, main window, plugin init |
| Main UI | `MainWindow.axaml`, `MainWindow.axaml.cs` | Avalonia-specific events, focus, screen positioning |
| View models | `ViewModel/`, `ViewModel/SettingPages/` | CommunityToolkit.Mvvm patterns |
| Settings pages | `Views/SettingPages/` | user-control pages loaded by tag switching |
| WPF bridge | `Views/Controls/WpfSettingsWindow.cs`, `WpfResources/` | hosts legacy WPF plugin settings/resources |

## LOCAL RULES
- Check `../AVALONIA_MIGRATION_CHECKLIST.md` before assuming parity with WPF.
- Match Avalonia patterns here: `ObservableObject`, attributes/commands, `UserControl` page composition, Avalonia events.
- `Program.cs` intentionally creates a WPF `System.Windows.Application`; do not remove it casually.
- Some settings/plugin flows explicitly branch between native Avalonia settings and WPF fallback hosting.

## ANTI-PATTERNS
- Do not copy WPF `Page`/`Frame` navigation patterns into this subtree.
- Do not claim feature parity from naming alone; verify behavior against checklist and code.
- Do not strip WPF compatibility resources unless you verified plugin settings still work.

## DIFF VS ROOT
- Root covers shared repo rules.
- This file covers Avalonia-only lifecycle, migration caveats, and WPF-interop constraints.
