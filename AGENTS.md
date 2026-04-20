# PROJECT KNOWLEDGE BASE

Windows launcher for search, commands, and plugins. Repo currently carries two app hosts: legacy WPF in `Flow.Launcher/` and the newer Avalonia host in `Flow.Launcher.Avalonia/`.

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| WPF app behavior | `Flow.Launcher/` | Main host, windows, settings shell, WPF-only UI rules |
| Avalonia app behavior | `Flow.Launcher.Avalonia/` | Avalonia host, WPF compatibility shim, migrated settings pages |
| Plugin lifecycle | `Flow.Launcher.Core/` | `PluginManager`, plugin loading, updates, manifest integration |
| Shared infra | `Flow.Launcher.Infrastructure/` | settings, storage, hotkeys, logging, fuzzy matching |
| Plugin SDK contract | `Flow.Launcher.Plugin/` | interfaces/models consumed by built-in and external plugins |
| Built-in plugins | `Plugins/` | 12 plugin projects, shared `Main.cs` + `plugin.json` structure |
| Test project | `Flow.Launcher.Test/` | NUnit 4 tests; some Explorer tests need Windows Search |
| CI / release process | `.github/` | workflow automation, release PRs, plugin publishing, issue templates |

## CHILD AGENTS
- `.github/AGENTS.md` — workflow and release automation only
- `Flow.Launcher/AGENTS.md` — WPF host-only rules
- `Flow.Launcher.Avalonia/AGENTS.md` — Avalonia host-only rules
- `Flow.Launcher.Core/AGENTS.md` — orchestration/update/plugin loading
- `Flow.Launcher.Infrastructure/AGENTS.md` — settings/storage/system helpers
- `Flow.Launcher.Plugin/AGENTS.md` — SDK contract guidance
- `Plugins/AGENTS.md` — shared built-in plugin conventions

## COMMANDS
```bash
nuget restore
dotnet build
dotnet build -c Release
dotnet test
dotnet test --filter "FullyQualifiedName~FuzzyMatcherTest"
```

Run outputs:
- `Output/Debug/Flow.Launcher.exe` — WPF
- `Output/Debug/Avalonia/Flow.Launcher.Avalonia.exe` — Avalonia

## VALIDATION
- Always finish implementation work with a final user test: start the relevant app host by directly executing the built `.exe`, then verify the debug log can be opened/read so runtime issues are visible.

## REPO CONVENTIONS
- C#: PascalCase for types/members/constants, camelCase locals/parameters, `_camelCase` private fields.
- No `this.` qualifier.
- File-scoped namespaces preferred.
- `var` when type is obvious.
- Allman braces always.
- Using directives outside namespace; system usings first.
- Prefer `is null`, null-propagation, null-coalescing.

## UI CONVENTIONS
- WPF uses `.xaml`; Avalonia uses `.axaml`.
- XAML/AXAML formatting: one attribute per line unless tiny, space before `/>`.
- Attribute order: `x:Class` → `xmlns` → key/name → layout → size → margin/padding → rest.
- Do not assume WPF and Avalonia pages are parity-complete; check `AVALONIA_MIGRATION_CHECKLIST.md` before porting behavior.

## MVVM SPLIT
- WPF commonly uses `BaseModel` and manual `OnPropertyChanged()`.
- Avalonia uses CommunityToolkit.Mvvm patterns such as `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`.
- Match the local host's pattern; do not “modernize” one host to the other in incidental changes.

## PLUGIN BASICS
- `Flow.Launcher.Plugin/` defines the contract; `Flow.Launcher.Core/PluginManager` owns discovery/load/init.
- Built-in plugin projects live in `Plugins/Flow.Launcher.Plugin.*`.
- Plugin metadata comes from `plugin.json`; user/community plugins also use `%AppData%/FlowLauncher/Plugins` style roots.

## GOTCHAS
- Build order matters: plugins before host outputs.
- Building can kill a running Flow Launcher process.
- Avalonia host still carries a WPF resource shim for plugin compatibility.
- Some tests require Windows Search service (`WSearch`).
- `Flow.Launcher.Test` includes real filesystem and slower perf-style tests; avoid assuming all tests are pure unit tests.

## KEY FILES
- `AVALONIA_MIGRATION_CHECKLIST.md` — migration status and missing parity
- `.editorconfig` — C#/VB style rules
- `Settings.XamlStyler` — XAML formatting rules
- `Directory.Build.props`, `Directory.Build.targets` — repo-wide build behavior
- `Flow.Launcher.Plugin/README.md` — SDK-facing docs
