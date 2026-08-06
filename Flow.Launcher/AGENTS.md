# Flow.Launcher (WPF) KNOWLEDGE BASE

Legacy WPF host. Owns the original search window, tray/window shell, and richer WPF settings/window surface.

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| App startup | `App.xaml`, `App.xaml.cs` | `Main()` + `OnStartup()`, single-instance, DI, plugin init |
| Main search UI | `MainWindow.xaml`, `MainWindow.xaml.cs` | focus, monitor placement, visibility, input behavior |
| View models | `ViewModel/` | `MainViewModel`, `ResultsViewModel`, `SettingWindowViewModel` |
| Settings host | `SettingWindow.xaml`, `SettingPages/` | NavigationView + Frame + Page-based settings shell |
| Resources/themes | `Resources/`, `Themes/` | WPF dictionaries, control templates, theme assets |

## LOCAL RULES
- Match WPF patterns here: `BaseModel`, manual property change notifications, code-behind where the host already uses it.
- Settings navigation is page/frame-based, not Avalonia-style `UserControl` swapping.
- This subtree still contains windows/dialogs not mirrored in Avalonia (`WelcomeWindow`, `ReleaseNotesWindow`, `ReportWindow`, richer shell behavior).
- Preserve single-instance and startup flow in `App.xaml.cs`; it does much more than window construction.

## ANTI-PATTERNS
- Do not import Avalonia MVVM or page-loading patterns into this subtree.
- Do not assume a WPF settings page already has an Avalonia peer.
- Do not simplify WPF resource dictionaries/control templates without checking downstream window/page usage.

## DIFF VS ROOT
- Root covers general style and repo layout.
- This file covers WPF-only lifecycle, windowing, settings-shell, and resource behavior.
