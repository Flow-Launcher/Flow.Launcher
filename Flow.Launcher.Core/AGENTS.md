# Flow.Launcher.Core KNOWLEDGE BASE

Core orchestration layer. Main responsibilities: plugin lifecycle, query routing, external/community plugin integration, updates, and shared resources/i18n.

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| Plugin lifecycle | `Plugin/PluginManager.cs` | discovery, load, init, query fanout |
| Plugin manifest parsing | `Plugin/PluginConfig.cs` | scans `plugin.json`, duplicate/version filtering |
| Loader implementation | `Plugin/PluginsLoader.cs` | .NET + external plugin loading |
| Community plugins | `ExternalPlugins/` | manifest/store integration |
| Updates | updater-related files | Squirrel + release logic |
| Resources/i18n | `Resource/`, related services | host/plugin resource merging |

## LOCAL RULES
- This project is coordination-heavy; read the full load/init/query path before changing a single method.
- `PluginManager` is a global/static boundary; small edits can affect both WPF and Avalonia hosts.
- Plugin directories come from preinstalled + user plugin roots; preserve both.
- Manifest behavior depends on `plugin.json` parsing and duplicate/version handling, not just assembly scanning.

## ANTI-PATTERNS
- Do not move SDK contract changes here; those belong in `Flow.Launcher.Plugin/`.
- Do not change plugin load/init flow without checking both app hosts and built-in plugins.
- Do not document host UI behavior here unless it directly touches plugin orchestration.
