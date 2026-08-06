# Flow.Launcher.Infrastructure KNOWLEDGE BASE

Shared infrastructure: settings, storage, logging, hotkeys, Win32 helpers, HTTP/image utilities, and fuzzy matching support.

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| User settings | `UserSettings/` | `Settings`, `DataLocation`, app/user data roots |
| Storage | `Storage/` | JSON/binary persistence, backup/atomic write behavior |
| Search matching | `StringMatcher.cs`, related models | fuzzy/acronym/pinyin behavior |
| Logging | `Logger/` | NLog wrapper and diagnostics |
| Hotkeys | `Hotkey/` | low-level keyboard integration |
| Windows helpers | helper/system wrappers | Win32 and shell integration |

## LOCAL RULES
- Treat storage code as data-safety critical; preserve backup and atomic-write behavior.
- `DataLocation` and constants affect plugin roots, caches, and settings locations across the whole app.
- Many consumers assume these helpers are stable and side-effect free; changes ripple widely.
- Search-quality changes often belong here, not in UI view models.

## ANTI-PATTERNS
- Do not tuck UI-specific logic into this project.
- Do not weaken path/storage recovery behavior for convenience.
- Do not change hotkey or Win32 helpers without checking both hosts and affected plugins/tests.
