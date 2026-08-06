# Flow.Launcher.Infrastructure/

The Infrastructure project provides the foundational services, utilities, and shared models used across the Flow.Launcher ecosystem. It serves as a bridge between high-level application logic and low-level system interactions.

## Responsibility

- **Persistence & Storage**: Manages serialization and deserialization of application data and settings using robust JSON/Binary storage mechanisms.
- **Fuzzy Matching**: Implements the core search logic, including fuzzy string matching, acronym detection, and multi-language support (e.g., Pinyin).
- **Global Hotkey Management**: Intercepts and processes system-wide keyboard events using low-level Windows hooks.
- **Configuration**: Provides a centralized, reactive `Settings` model that drives the application's behavior and appearance.
- **Logging**: Offers a unified logging interface wrapping NLog for error reporting and debugging.
- **System Integration**: Wraps complex Win32 API calls and provides helpers for file system operations, image processing, and environment management.

## Design Patterns

- **Persistence Framework (Generic Storage)**: `JsonStorage<T>` uses a generic approach to provide consistent serialization logic, including atomic writes and backup recovery.
- **Reactive Model**: The `Settings` class uses the MVVM pattern (via `CommunityToolkit.Mvvm`) to notify the UI of configuration changes.
- **Static Service/Singleton**: Core utilities like `Log`, `DataLocation`, and `Win32Helper` are implemented as static services for global accessibility.
- **Dependency Injection**: Services like `StringMatcher` are registered and resolved via `CommunityToolkit.Mvvm.DependencyInjection` to decouple components.
- **Template Method**: `JsonStorage<T>` defines the save/load algorithm, while derived classes like `FlowLauncherJsonStorage<T>` specialize the storage location.

## Key Classes

### Configuration & Data
- **`UserSettings.Settings`**: The primary configuration model. It implements `IHotkeySettings` and holds hundreds of user-configurable properties. It is reactive, triggering UI updates immediately upon property change.
- **`UserSettings.DataLocation`**: Encapsulates the logic for determining where application data is stored (Roaming vs. Portable mode).
- **`Constant`**: Centralized repository for application-wide constants, paths, and URLs.

### Search Engine
- **`StringMatcher`**: The heart of Flow's search capability. It combines acronym matching and fuzzy substring matching. It supports search precision tuning and leverages `IAlphabet` for localized character translation.
- **`MatchResult`**: A model representing the outcome of a search match, containing the score and the indices of matched characters for UI highlighting.

### Storage Systems
- **`Storage.JsonStorage<T>`**: A robust JSON storage implementation featuring:
    - **Atomic Writes**: Uses `.tmp` files and `File.Replace` to prevent data corruption during crashes.
    - **Backup System**: Automatically maintains `.bak` files and timestamped backups if loading fails.
- **`Storage.FlowLauncherJsonStorage<T>`**: A specialization of `JsonStorage` that automatically routes files to the appropriate `UserData` directory.

### Logging & Diagnostics
- **`Logger.Log`**: A static wrapper for NLog. It supports asynchronous file logging and debug string output. It includes a `Demystify` step for exception stacks to make them more readable.
- **`Stopwatch`**: High-resolution timing utility for performance profiling.

### Input & System
- **`Hotkey.GlobalHotkey`**: Manages low-level `WH_KEYBOARD_LL` hooks to intercept hotkeys before they reach other applications.
- **`Win32Helper`**: Provides a clean C# interface for complex P/Invoke operations, including window management, shell execution, and system font retrieval.

## Data & Control Flow

### Search & Result Flow
1. **Input**: A query string enters from the UI.
2. **Preprocessing**: `StringMatcher` trims the query and optionally translates it via `PinyinAlphabet` or other `IAlphabet` implementations.
3. **Matching**: 
    - **Acronym Match**: Checks if query characters match the starts of words or uppercase letters.
    - **Fuzzy Match**: Performs substring matching and character distance calculation.
4. **Scoring**: A score is calculated based on match length, position, and closeness of characters.
5. **Output**: A `MatchResult` is returned to the `PluginManager` to be ranked against other results.

### Settings Lifecycle
1. **Loading**: On startup, `FlowLauncherJsonStorage<Settings>` reads `Settings.json`. If corrupt, it attempts to restore from `Settings.json.bak`.
2. **Injection**: The `Settings` instance is registered in the `Ioc` container.
3. **Modification**: UI components bind to `Settings` properties. Changes trigger `OnPropertyChanged`.
4. **Persistence**: The `Save()` method is called (often triggered by application exit or manual save), executing an atomic write to disk.

## Integration Points

- **NLog**: Backend for the `Log` service.
- **CommunityToolkit.Mvvm**: Used for the `Ioc` container and `ObservableObject` base classes.
- **CsWin32 / PInvoke**: Deep integration with Windows APIs for hotkeys, file explorer interaction, and UI rendering hints.
- **Flow.Launcher.Plugin**: Infrastructure provides the concrete implementations for many interfaces defined in the Plugin SDK.
- **System.Text.Json**: The primary serialization engine for all storage components.
