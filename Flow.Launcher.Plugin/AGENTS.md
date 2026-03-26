# Flow.Launcher.Plugin KNOWLEDGE BASE

Public SDK contract for plugin authors and for host/plugin integration. This is the stable boundary between app internals and plugins.

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| Core plugin interfaces | `Interfaces/` | `IPlugin`, `IAsyncPlugin`, capability interfaces |
| Public models | root models, `SharedModels/` | `Result`, `Query`, metadata, settings-facing types |
| Shared commands/helpers | `SharedCommands/` | reusable SDK-side pieces |
| Docs | `README.md` | package/plugin authoring baseline |

## LOCAL RULES
- Prefer additive, compatibility-preserving SDK changes.
- Interface changes here affect built-in plugins, external plugins, tests, and both app hosts.
- `PluginMetadata`, `PluginInitContext`, `IPublicAPI`, `Result`, and `Query` are high-blast-radius types.
- Keep examples and docs aligned with actual built-in plugin usage patterns.

## ANTI-PATTERNS
- Do not move host implementation details into the SDK.
- Do not make contract changes in `Flow.Launcher.Core/` and forget to mirror them here.
- Do not assume external plugins can absorb breaking changes casually.
