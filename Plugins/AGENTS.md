# Plugins KNOWLEDGE BASE

Built-in plugin area. Shared shape is stronger than per-plugin divergence, so keep common rules here and avoid plugin-local children unless a plugin develops truly unique guidance.

## SHARED STRUCTURE
- One project per plugin: `Plugins/Flow.Launcher.Plugin.*`
- Typical files: `Main.cs`, `plugin.json`, plugin `.csproj`, `Images/`, often `Settings.cs`, sometimes `Languages/*.xaml`
- Entry class is usually `Main`, implementing some mix of `IPlugin` / `IAsyncPlugin` / `ISettingProvider` / `IPluginI18n` / `IContextMenu`
- Output/build behavior is broadly consistent across plugins, including manifest/resource copy steps and Avalonia output copy targets in many projects

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| Entry/query logic | each plugin `Main.cs` | sync vs async behavior lives here |
| Manifest | each plugin `plugin.json` | metadata, action keyword(s), icon, execute file |
| Settings model | `Settings.cs` or equivalent | usually loaded through API storage helpers |
| Plugin UI/settings | plugin views/viewmodels if present | some have WPF + Avalonia settings paths |

## LOCAL RULES
- Follow the dominant local pattern of the target plugin before copying another plugin's style.
- `plugin.json` is not perfectly uniform: some plugins use `ActionKeyword`, others `ActionKeywords`.
- Settings location is not perfectly uniform either; e.g. BrowserBookmark keeps settings under `Models/Settings.cs`.
- Treat Avalonia settings support as plugin-specific: some plugins expose both paths, some fall back, Explorer is Avalonia-only for settings.
- Resource handling varies: most plugins use `Languages/*.xaml`; `WindowsSettings` uses `.resx` resources instead.

## NOTABLE EXCEPTIONS
- `Explorer` adds search-provider complexity and Avalonia-only settings panel behavior.
- `Program`, `WebSearch`, `Explorer`, `PluginsManager` are async plugins.
- `PluginIndicator` uniquely implements `IHomeQuery`.
- Several plugins still carry WPF/WinForms build properties for host integration.

## ANTI-PATTERNS
- Do not create plugin-local AGENTS files just because a plugin is large; add one only when it gains rules not shared by the rest.
- Do not normalize `plugin.json` or settings layout across plugins as incidental cleanup.
- Do not assume every plugin has both WPF and Avalonia settings UI.
