# Flow.Launcher Avalonia Migration Checklist

> **Overall Progress: ~60-65%**  
> Last Updated: February 2026

---

## Table of Contents
1. [Windows & Dialogs](#1-windows--dialogs)
2. [Settings Pages](#2-settings-pages)
3. [Custom Controls](#3-custom-controls)
4. [ViewModels](#4-viewmodels)
5. [Helpers](#5-helpers)
6. [Converters](#6-converters)
7. [Core Features](#7-core-features)
8. [Theming](#8-theming)
9. [Animations](#9-animations)

---

## 1. Windows & Dialogs

| Window | WPF File | Status | Notes |
|--------|----------|--------|-------|
| MainWindow | `MainWindow.xaml` | :white_check_mark: Done | Core search UI working |
| SettingsWindow | `SettingWindow.xaml` | :white_check_mark: Done | Navigation frame working |
| HotkeyControl | `HotkeyControl.xaml` | :white_check_mark: Done | Button control |
| HotkeyRecorderDialog | `HotkeyControlDialog.xaml` | :white_check_mark: Done | Global hook + modifier tracking |
| ResultListBox | `ResultListBox.xaml` | :white_check_mark: Done | Results display |
| PreviewPanel | `PreviewPanel.xaml` | :white_check_mark: Done | Result preview |
| WelcomeWindow | `WelcomeWindow.xaml` | :x: Missing | First-run setup wizard |
| ReportWindow | `ReportWindow.xaml` | :x: Missing | Error reporting dialog |
| ReleaseNotesWindow | `ReleaseNotesWindow.xaml` | :x: Missing | Changelog display |
| SelectBrowserWindow | `SelectBrowserWindow.xaml` | :x: Missing | Browser selection |
| SelectFileManagerWindow | `SelectFileManagerWindow.xaml` | :x: Missing | File manager selection |
| PluginUpdateWindow | `PluginUpdateWindow.xaml` | :x: Missing | Plugin update progress |
| MessageBoxEx | `MessageBoxEx.xaml` | :x: Missing | Custom message box |
| Msg | `Msg.xaml` | :x: Missing | Simple message dialog |
| MsgWithButton | `MsgWithButton.xaml` | :x: Missing | Message with action button |
| ProgressBoxEx | `ProgressBoxEx.xaml` | :x: Missing | Progress dialog |
| ActionKeywords | `ActionKeywords.xaml` | :x: Missing | Action keyword editor (using dialog instead) |
| CustomQueryHotkeySetting | `CustomQueryHotkeySetting.xaml` | :x: Missing | Custom hotkey dialog |
| CustomShortcutSetting | `CustomShortcutSetting.xaml` | :x: Missing | Shortcut editor |

**Progress: 6/19 (32%)**

---

## 2. Settings Pages

### 2.1 General Settings (`SettingsPaneGeneral.xaml` - 538 lines)
**Avalonia: ~270 lines (~95% DONE)**

| Setting | Status | Binding Property |
|---------|--------|------------------|
| **Startup Section** |
| Start on system startup | :white_check_mark: Done | `StartOnStartup` |
| Use logon task | :white_check_mark: Done | `UseLogonTaskForStartup` |
| Hide on startup | :white_check_mark: Done | `HideOnStartup` |
| **Behavior Section** |
| Hide when lose focus | :white_check_mark: Done | `HideWhenDeactivated` |
| Hide notify icon | :white_check_mark: Done | `HideNotifyIcon` |
| Show at topmost | :white_check_mark: Done | `ShowAtTopmost` |
| Ignore hotkeys on fullscreen | :white_check_mark: Done | `IgnoreHotkeysOnFullscreen` |
| Always preview | :white_check_mark: Done | `AlwaysPreview` |
| **Position Section** |
| Search window position | :white_check_mark: Done | `SelectedSearchWindowScreen` |
| Search window align | :white_check_mark: Done | `SelectedSearchWindowAlign` |
| Custom position X/Y | :x: Missing | `Settings.CustomWindowLeft/Top` |
| **Search Section** |
| Query search precision | :white_check_mark: Done | `SelectedSearchPrecision` |
| Last query mode | :white_check_mark: Done | `SelectedLastQueryMode` |
| Search delay toggle | :white_check_mark: Done | `SearchQueryResultsWithDelay` |
| Search delay time | :white_check_mark: Done | `SearchDelayTime` |
| **Home Page Section** |
| Show home page | :white_check_mark: Done | `ShowHomePage` |
| History results for home | :white_check_mark: Done | `ShowHistoryResultsForHomePage` |
| History results count | :white_check_mark: Done | `MaxHistoryResultsToShow` |
| **Updates Section** |
| Auto updates | :white_check_mark: Done | `AutoUpdates` |
| Auto update plugins | :white_check_mark: Done | `AutoUpdatePlugins` |
| **Miscellaneous** |
| Auto restart after changing | :white_check_mark: Done | `AutoRestartAfterChanging` |
| Show unknown source warning | :white_check_mark: Done | `ShowUnknownSourceWarning` |
| Always start English | :white_check_mark: Done | `AlwaysStartEn` |
| Use Pinyin | :white_check_mark: Done | `ShouldUsePinyin` |
| **Language Section** |
| Language selector | :white_check_mark: Done | `SelectedLanguage` |
| **Paths** |
| Python directory | :white_check_mark: Done | `PythonPath` (display) |
| Node directory | :white_check_mark: Done | `NodePath` (display) |
| **Not Yet Implemented** |
| Select browser | :x: Missing | Opens `SelectBrowserWindow` |
| Select file manager | :x: Missing | Opens `SelectFileManagerWindow` |
| Portable mode | :x: Missing | Toggle |
| Dialog jump settings | :x: Missing | ExCard with nested options |
| Double pinyin settings | :x: Missing | ExCard with schema selector |
| Korean IME settings | :x: Missing | ExCard with registry toggle |

### 2.2 Theme Settings (`SettingsPaneTheme.xaml` - 803 lines)
**Avalonia: ~70 lines (~35% DONE)**

| Setting | Status | Notes |
|---------|--------|-------|
| **Theme Selection** |
| Theme list | :x: Missing | ListView with theme previews |
| Theme preview | :x: Missing | Live preview panel |
| **Window Settings** |
| Window size slider | :x: Missing | `Settings.WindowSize` |
| Window height slider | :x: Missing | `Settings.WindowHeightSize` |
| Item height slider | :x: Missing | `Settings.ItemHeightSize` |
| **Font Settings** |
| Query box font | :x: Missing | `Settings.QueryBoxFont` (font family) |
| Query box font size | :white_check_mark: Done | `Settings.QueryBoxFontSize` |
| Result font | :x: Missing | `Settings.ResultFont` (font family) |
| Result font size | :white_check_mark: Done | `Settings.ResultItemFontSize` |
| Result sub font size | :x: Missing | `Settings.ResultSubItemFontSize` |
| **Appearance** |
| Color scheme | :white_check_mark: Done | Light/Dark/System |
| Animation speed | :x: Missing | `Settings.AnimationSpeed` |
| Use clock | :x: Missing | `Settings.UseDate` |
| Clock format | :x: Missing | `Settings.TimeFormat` |
| Date format | :x: Missing | `Settings.DateFormat` |
| Use glyph icons | :white_check_mark: Done | `Settings.UseGlyphIcons` |
| Max results | :white_check_mark: Done | `Settings.MaxResultsToShow` |
| **Backdrop** |
| Use system backdrop | :x: Missing | `Settings.UseSystemBackdrop` |
| Backdrop type | :x: Missing | Mica/Acrylic/etc |
| **Icon Settings** |
| Icon theme | :x: Missing | `Settings.ColorScheme` |
| Double-click icon action | :x: Missing | `Settings.DoubleClickIconAction` |

### 2.3 Hotkey Settings (`SettingsPaneHotkey.xaml` - 463 lines)
**Avalonia: ~26 lines (~10% DONE)**

| Hotkey | Status | Setting Property |
|--------|--------|------------------|
| Toggle Flow Launcher | :white_check_mark: Done | `Settings.Hotkey` |
| Preview hotkey | :x: Missing | `Settings.PreviewHotkey` |
| Auto-complete hotkey | :x: Missing | `Settings.AutoCompleteHotkey` |
| Select next item | :x: Missing | `Settings.SelectNextItemHotkey` |
| Select prev item | :x: Missing | `Settings.SelectPrevItemHotkey` |
| Select next page | :x: Missing | `Settings.SelectNextPageHotkey` |
| Select prev page | :x: Missing | `Settings.SelectPrevPageHotkey` |
| Open result hotkeys (1-9) | :x: Missing | `Settings.OpenResultHotkey[1-9]` |
| Open context menu | :x: Missing | `Settings.OpenContextMenuHotkey` |
| Cycle history up | :x: Missing | `Settings.CycleHistoryUpHotkey` |
| Cycle history down | :x: Missing | `Settings.CycleHistoryDownHotkey` |
| **Custom Query Hotkeys** |
| Custom query list | :x: Missing | List of user-defined hotkeys |
| Add custom hotkey | :x: Missing | Opens editor dialog |

### 2.4 Plugin Settings (`SettingsPanePlugins.xaml` - 141 lines)
**Avalonia: ~170 lines (~90% DONE)**

| Feature | Status | Notes |
|---------|--------|-------|
| Plugin list | :white_check_mark: Done | Expandable list view |
| Plugin icon | :white_check_mark: Done | Shows icon |
| Plugin name | :white_check_mark: Done | Shows name |
| Plugin enable/disable | :white_check_mark: Done | Toggle switch |
| Plugin details panel | :white_check_mark: Done | Description, author, version, init/query time |
| Plugin settings UI | :white_check_mark: Done | `IPluginSettingProvider` integration (Avalonia + WPF fallback) |
| Action keywords editor | :white_check_mark: Done | Dialog-based editor |
| Plugin priority | :white_check_mark: Done | Number box |
| Search delay per plugin | :white_check_mark: Done | Number box |
| Home page enable/disable | :white_check_mark: Done | Toggle switch |
| Display mode selector | :white_check_mark: Done | OnOff/Priority/SearchDelay/HomeOnOff |
| Plugin directory button | :white_check_mark: Done | Opens folder |
| Source code link | :white_check_mark: Done | Opens website |
| Uninstall plugin | :white_check_mark: Done | With confirmation dialog |
| Help dialog | :white_check_mark: Done | Priority/search delay/home tips |

### 2.5 Plugin Store (`SettingsPanePluginStore.xaml` - 400 lines)
**Avalonia: ~200 lines (~95% DONE)**

| Feature | Status | Notes |
|---------|--------|-------|
| Plugin search | :white_check_mark: Done | Search box with fuzzy search |
| Plugin grid | :white_check_mark: Done | ItemsRepeater with UniformGridLayout |
| Plugin card | :white_check_mark: Done | Icon, name, description, author |
| Install plugin | :white_check_mark: Done | Download and install |
| Update plugin | :white_check_mark: Done | Update available indicator + button |
| Uninstall plugin | :white_check_mark: Done | Remove plugin |
| Plugin filter | :white_check_mark: Done | Filter by language (.NET/Python/Node/Executable) |
| Refresh plugins | :white_check_mark: Done | Button to refresh manifest |
| Check updates | :white_check_mark: Done | Check for plugin updates |
| Local install | :white_check_mark: Done | Install from .zip file |
| Loading indicator | :white_check_mark: Done | Progress ring while loading |
| Flyout details | :white_check_mark: Done | Full details on click |
| Website/Source links | :white_check_mark: Done | Open in browser |

### 2.6 Proxy Settings (`SettingsPaneProxy.xaml` - 80 lines)
**Avalonia: ~47 lines (~100% DONE)**

| Setting | Status | Notes |
|---------|--------|-------|
| Enable proxy | :white_check_mark: Done | Toggle |
| Proxy server | :white_check_mark: Done | Text input |
| Proxy port | :white_check_mark: Done | Number input |
| Proxy username | :white_check_mark: Done | Text input |
| Proxy password | :white_check_mark: Done | Password input (masked) |

### 2.7 About Settings (`SettingsPaneAbout.xaml` - 184 lines)
**Avalonia: ~25 lines (~40% DONE)**

| Feature | Status | Notes |
|---------|--------|-------|
| Version display | :white_check_mark: Done | Shows version |
| Check for updates | :x: Missing | Button + status |
| Homepage link | :white_check_mark: Done | Button |
| Documentation link | :x: Missing | HyperLink |
| GitHub link | :white_check_mark: Done | Button |
| Discord link | :x: Missing | HyperLink |
| Release notes | :x: Missing | Opens `ReleaseNotesWindow` |
| Open logs folder | :x: Missing | Button |
| Open settings folder | :x: Missing | Button |

**Settings Progress: ~1100/2609 lines (~42%)**

---

## 3. Custom Controls

| Control | WPF File | Status | Description |
|---------|----------|--------|-------------|
| Card | `Card.xaml.cs` | :white_check_mark: Done | Settings card with icon, title, subtitle |
| ExCard | `ExCard.xaml.cs` | :white_check_mark: Done | Expandable card with nested content |
| CardGroup | `CardGroup.xaml.cs` | :white_check_mark: Done | Groups cards with rounded corners |
| HotkeyControl | `HotkeyControl.xaml` | :white_check_mark: Done | Hotkey display and recording |
| HotkeyRecorderDialog | `HotkeyControlDialog.xaml` | :white_check_mark: Done | Dialog for recording hotkeys |
| InfoBar | `InfoBar.xaml.cs` | :x: Missing | Information/warning banner |
| HyperLink | `HyperLink.xaml.cs` | :x: Missing | Clickable link control |
| HotkeyDisplay | `HotkeyDisplay.xaml.cs` | :x: Missing | Displays hotkey as key badges |
| InstalledPluginDisplay | `InstalledPluginDisplay.xaml.cs` | :x: Missing | Plugin info card (replaced by Expander) |
| InstalledPluginDisplayKeyword | `InstalledPluginDisplayKeyword.xaml.cs` | :x: Missing | Keyword badge (integrated) |
| InstalledPluginDisplayBottomData | `InstalledPluginDisplayBottomData.xaml.cs` | :x: Missing | Plugin metadata footer (integrated) |

**Progress: 5/9 (56%)**

> Note: Using FluentAvalonia's `SettingsExpander` for most settings pages instead of custom Card controls. Plugin display uses native Avalonia Expander.

---

## 4. ViewModels

| ViewModel | WPF Lines | Avalonia Lines | Status | Notes |
|-----------|-----------|----------------|--------|-------|
| MainViewModel | 2292 | ~490 | :yellow_circle: 21% | Core query works, missing history/clipboard |
| ResultsViewModel | ~200 | ~150 | :white_check_mark: 75% | Core functionality done |
| ResultViewModel | ~300 | ~200 | :white_check_mark: 67% | Basic display done |
| SettingWindowViewModel | ~100 | ~50 | :yellow_circle: 50% | Navigation works |
| GeneralSettingsViewModel | ~100 | ~390 | :white_check_mark: 95% | Most settings implemented |
| ThemeSettingsViewModel | ~150 | ~116 | :yellow_circle: 77% | Basic theme + fonts done |
| HotkeySettingsViewModel | ~200 | ~31 | :yellow_circle: 15% | Only toggle hotkey done |
| PluginsSettingsViewModel | ~300 | ~220 | :white_check_mark: 73% | Full plugin management |
| PluginStoreSettingsViewModel | ~250 | ~200 | :white_check_mark: 80% | Store functionality complete |
| PluginStoreItemViewModel | ~100 | ~113 | :white_check_mark: 95% | Item display + actions |
| PluginItemViewModel | N/A | ~277 | :white_check_mark: 90% | Individual plugin management |
| ProxySettingsViewModel | ~80 | ~81 | :white_check_mark: 100% | All proxy settings |
| AboutSettingsViewModel | ~100 | ~28 | :yellow_circle: 28% | Basic version + links |
| WelcomeViewModel | ~80 | 0 | :x: 0% | Not started |
| SelectBrowserViewModel | ~50 | 0 | :x: 0% | Not started |
| SelectFileManagerViewModel | ~50 | 0 | :x: 0% | Not started |

### MainViewModel Missing Features
- [ ] History cycling (up/down arrow)
- [ ] Clipboard paste handling
- [ ] Auto-complete suggestions
- [ ] Text selection handling
- [ ] Preview panel toggle
- [ ] Dialog jump feature
- [ ] Game mode detection
- [ ] Window position memory
- [ ] IME mode control

**Progress: ~45%**

---

## 5. Helpers

| Helper | Status | Description |
|--------|--------|-------------|
| HotKeyMapper | :white_check_mark: Done | Hotkey registration |
| ImageLoader | :white_check_mark: Done | Image caching/loading |
| FontLoader | :white_check_mark: Done | Font loading |
| GlobalHotkey | :white_check_mark: Done | In Infrastructure project |
| TextBlockHelper | :white_check_mark: Done | Text formatting helpers |
| AutoStartup | :x: Missing | Windows startup registration |
| SingleInstance | :x: Missing | Prevent multiple instances |
| ErrorReporting | :x: Missing | Error logging/reporting |
| ExceptionHelper | :x: Missing | Exception formatting |
| SingletonWindowOpener | :x: Missing | Manage singleton windows |
| WallpaperPathRetrieval | :x: Missing | Get desktop wallpaper |
| WindowsMediaPlayerHelper | :x: Missing | Preview audio files |
| DataWebRequestFactory | :x: Missing | Web requests with proxy |
| SyntaxSugars | :x: Missing | Extension methods |

**Progress: 5/13 (38%)**

---

## 6. Converters

| Converter | Status | Description |
|-----------|--------|-------------|
| BoolToIsVisibleConverter | :white_check_mark: Done | Boolean to IsVisible |
| TranslationConverter | :white_check_mark: Done | Localization converter |
| CommonConverters | :white_check_mark: Done | Various common converters |
| HighlightTextConverter | :x: Missing | Bold gold highlighting for matched characters |
| QuerySuggestionBoxConverter | :yellow_circle: Stub | Not implemented |
| TextConverter | :x: Missing | Text transformations |
| SizeRatioConverter | :x: Missing | Size calculations |
| BadgePositionConverter | :x: Missing | Badge positioning |
| StringToKeyBindingConverter | :x: Missing | String to key gesture |
| OrdinalConverter | :x: Missing | Number to ordinal (1st, 2nd) |
| OpenResultHotkeyVisibilityConverter | :x: Missing | Hotkey badge visibility |
| IconRadiusConverter | :x: Missing | Icon corner radius |
| DiameterToCenterPointConverter | :x: Missing | Circle center calculation |
| DateTimeFormatToNowConverter | :x: Missing | Date/time formatting |
| BorderClipConverter | :x: Missing | Border clipping geometry |
| BoolToIMEConversionModeConverter | :x: Missing | IME mode conversion |

**Progress: 3/16 (19%)**

---

## 7. Core Features

### Search & Results
| Feature | Status | Notes |
|---------|--------|-------|
| Basic query | :white_check_mark: Done | Text input triggers search |
| Results display | :white_check_mark: Done | Shows results with icons |
| Result selection | :white_check_mark: Done | Keyboard navigation |
| Result activation | :white_check_mark: Done | Enter to execute |
| Context menu | :white_check_mark: Done | Shift+Enter |
| Text highlighting | :x: Missing | Match highlighting in results |
| Auto-complete | :x: Missing | Tab to complete |
| Query suggestions | :x: Missing | Suggestion dropdown |
| History cycling | :x: Missing | Up/Down through history |
| Clipboard paste | :x: Missing | Paste and search |

### Window Management
| Feature | Status | Notes |
|---------|--------|-------|
| Show/Hide toggle | :white_check_mark: Done | Global hotkey works |
| Window dragging | :white_check_mark: Done | Drag to move |
| Hide on focus loss | :white_check_mark: Done | Deactivate handling |
| Position memory | :x: Missing | Remember last position |
| Multi-monitor | :x: Missing | Position on specific monitor |
| Topmost mode | :white_check_mark: Done | Always on top option |

### System Integration
| Feature | Status | Notes |
|---------|--------|-------|
| System tray | :white_check_mark: Done | Icon with menu |
| Global hotkey | :white_check_mark: Done | Toggle window |
| Auto-start | :x: Missing | Start with Windows |
| Single instance | :x: Missing | Prevent duplicates |
| Portable mode | :x: Missing | Run from USB |

### Plugin System
| Feature | Status | Notes |
|---------|--------|-------|
| Load plugins | :white_check_mark: Done | Via PluginManager |
| Plugin queries | :white_check_mark: Done | Action keywords work |
| Plugin settings UI | :white_check_mark: Done | IPluginSettingProvider (Avalonia native + WPF fallback) |
| Plugin install | :white_check_mark: Done | From store |
| Plugin update | :white_check_mark: Done | Check/apply updates |
| Plugin uninstall | :white_check_mark: Done | With confirmation |
| Python plugins | :white_check_mark: Partial | Python path config (display only) |
| Node plugins | :white_check_mark: Partial | Node path config (display only) |

**Progress: ~55%**

---

## 8. Theming

| Feature | Status | Notes |
|---------|--------|-------|
| Light/Dark/System | :white_check_mark: Done | Basic switching |
| Custom themes | :x: Missing | Load from .xaml files |
| Theme list | :x: Missing | Browse available themes |
| Theme preview | :x: Missing | Live preview in settings |
| Font customization | :yellow_circle: Partial | Font sizes done, font families missing |
| Size customization | :x: Missing | Window/item sizes |
| Animation speed | :x: Missing | Transition speed |
| Backdrop effects | :x: Missing | Mica/Acrylic |
| Icon themes | :x: Missing | Glyph/image icons |

**Progress: ~20%**

---

## 9. Animations

| Animation | Status | Notes |
|-----------|--------|-------|
| Window show/hide | :x: Missing | Fade/slide animation |
| Result list | :x: Missing | Item entrance animation |
| Context menu | :x: Missing | Menu slide animation |
| Settings navigation | :x: Missing | Page transitions |
| Progress indicators | :white_check_mark: Done | Loading spinners (FluentAvalonia) |

**Progress: ~10%**

---

## Priority Recommendations

### High Priority (Core UX)
1. [ ] **HighlightTextConverter** - Search match highlighting
2. [ ] **History cycling** - Up/Down arrow through history
3. [x] **GeneralSettingsPage** - Essential settings (startup, behavior) - DONE
4. [x] **Card/ExCard controls** - Required for all settings pages - DONE (using FluentAvalonia SettingsExpander)
5. [x] **Hide on focus loss** - Expected behavior - DONE

### Medium Priority (Feature Completeness)
6. [ ] **HotkeySettingsPage** - All keyboard shortcuts (only toggle done)
7. [ ] **ThemeSettingsPage** - Theme selection and customization (partial)
8. [x] **Plugin settings UI** - IPluginSettingProvider integration - DONE
9. [x] **Plugin Store** - Install/update plugins - DONE
10. [ ] **Auto-start** - Windows startup registration

### Lower Priority (Polish)
11. [ ] **WelcomeWindow** - First-run experience
12. [ ] **Animations** - Transitions and effects
13. [ ] **Message dialogs** - Custom message boxes
14. [ ] **Error reporting** - ReportWindow
15. [ ] **Backdrop effects** - Mica/Acrylic

---

## File Reference

### WPF Source Files
```
Flow.Launcher/
├── MainWindow.xaml (+ .cs)
├── SettingWindow.xaml
├── WelcomeWindow.xaml
├── ReportWindow.xaml
├── ... (19 total windows)
├── SettingPages/Views/
│   ├── SettingsPaneGeneral.xaml (538 lines)
│   ├── SettingsPaneTheme.xaml (803 lines)
│   ├── SettingsPaneHotkey.xaml (463 lines)
│   ├── SettingsPanePlugins.xaml (141 lines)
│   ├── SettingsPanePluginStore.xaml (400 lines)
│   ├── SettingsPaneProxy.xaml (80 lines)
│   └── SettingsPaneAbout.xaml (184 lines)
├── ViewModel/
│   └── MainViewModel.cs (2292 lines)
├── Helper/ (10 files)
├── Converters/ (14 files)
└── Resources/Controls/ (10 custom controls)
```

### Avalonia Target Files
```
Flow.Launcher.Avalonia/
├── MainWindow.axaml (+ .cs)
├── Views/
│   ├── ResultListBox.axaml
│   ├── PreviewPanel.axaml
│   ├── Controls/
│   │   ├── HotkeyControl.axaml
│   │   ├── HotkeyRecorderDialog.axaml
│   │   ├── Card.axaml (+ .cs)
│   │   ├── ExCard.axaml (+ .cs)
│   │   └── CardGroup.axaml (+ .cs)
│   └── SettingPages/
│       ├── SettingsWindow.axaml
│       ├── GeneralSettingsPage.axaml (~270 lines)
│       ├── ThemeSettingsPage.axaml (~70 lines)
│       ├── HotkeySettingsPage.axaml (~26 lines)
│       ├── PluginsSettingsPage.axaml (~170 lines)
│       ├── PluginStoreSettingsPage.axaml (~200 lines)
│       ├── ProxySettingsPage.axaml (~47 lines)
│       └── AboutSettingsPage.axaml (~25 lines)
├── ViewModel/
│   ├── MainViewModel.cs (~490 lines)
│   ├── SettingPages/
│   │   ├── GeneralSettingsViewModel.cs (~390 lines)
│   │   ├── ThemeSettingsViewModel.cs (~116 lines)
│   │   ├── HotkeySettingsViewModel.cs (~31 lines)
│   │   ├── PluginsSettingsViewModel.cs (~220 lines)
│   │   ├── PluginStoreSettingsViewModel.cs (~200 lines)
│   │   ├── PluginStoreItemViewModel.cs (~113 lines)
│   │   ├── PluginItemViewModel.cs (~277 lines)
│   │   ├── ProxySettingsViewModel.cs (~81 lines)
│   │   └── AboutSettingsViewModel.cs (~28 lines)
│   └── ...
├── Helper/
│   ├── FontLoader.cs
│   ├── GlobalHotkey.cs
│   ├── HotKeyMapper.cs
│   ├── ImageLoader.cs
│   └── TextBlockHelper.cs
├── Converters/
│   ├── BoolToIsVisibleConverter.cs
│   ├── CommonConverters.cs
│   └── TranslationConverter.cs
└── Themes/
    ├── Base.axaml
    └── Resources.axaml
```

---

## Build & Test

```bash
# Build
dotnet build Flow.Launcher.Avalonia/Flow.Launcher.Avalonia.csproj

# Run
./Output/Debug/Avalonia/Flow.Launcher.Avalonia.exe
```
