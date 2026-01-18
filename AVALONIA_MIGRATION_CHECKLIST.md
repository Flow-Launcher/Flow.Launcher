# Flow.Launcher Avalonia Migration Checklist

> **Overall Progress: ~30-35%**  
> Last Updated: January 2026

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
| ActionKeywords | `ActionKeywords.xaml` | :x: Missing | Action keyword editor |
| CustomQueryHotkeySetting | `CustomQueryHotkeySetting.xaml` | :x: Missing | Custom hotkey dialog |
| CustomShortcutSetting | `CustomShortcutSetting.xaml` | :x: Missing | Shortcut editor |

**Progress: 5/19 (26%)**

---

## 2. Settings Pages

### 2.1 General Settings (`SettingsPaneGeneral.xaml` - 538 lines)
**Avalonia: 71 lines (~13%)**

| Setting | Status | Binding Property |
|---------|--------|------------------|
| **Startup Section** |
| Start on system startup | :x: Missing | `StartFlowLauncherOnSystemStartup` |
| Use logon task | :x: Missing | `UseLogonTaskForStartup` |
| Hide on startup | :x: Missing | `Settings.HideOnStartup` |
| **Behavior Section** |
| Hide when lose focus | :x: Missing | `Settings.HideWhenDeactivated` |
| Hide notify icon | :x: Missing | `Settings.HideNotifyIcon` |
| Show at topmost | :x: Missing | `Settings.ShowAtTopmost` |
| **Position Section** |
| Search window position | :x: Missing | `Settings.SearchWindowScreen` |
| Custom position X/Y | :x: Missing | `Settings.CustomWindowLeft/Top` |
| Remember last position | :x: Missing | `Settings.RememberLastLaunchLocation` |
| **Search Section** |
| Max results shown | :x: Missing | `Settings.MaxResultsToShow` |
| Clear input on close | :x: Missing | `Settings.ClearInputOnLaunch` |
| Auto-complete text | :white_check_mark: Done | `Settings.AutoCompleteText` |
| Keep search text | :x: Missing | `Settings.KeepMaxResults` |
| Query search precision | :x: Missing | `Settings.QuerySearchPrecision` |
| **Input Section** |
| Always start English mode | :x: Missing | `Settings.AlwaysStartEn` |
| First search delay | :x: Missing | `Settings.FirstSearchDelay` |
| Input delay | :x: Missing | `Settings.InputDelay` |
| **Language Section** |
| Language selector | :white_check_mark: Done | `Settings.Language` |
| **Updates Section** |
| Auto-update interval | :x: Missing | `Settings.UpdateCheckInterval` |
| Check for updates | :x: Missing | `UpdateApp` command |
| **Advanced Section** |
| Python directory | :x: Missing | `Settings.PluginSettings.PythonExecutablePath` |
| Node directory | :x: Missing | `Settings.PluginSettings.NodeExecutablePath` |
| Plugin directory | :x: Missing | `Settings.PluginSettings.PluginDirectory` |
| Select browser | :x: Missing | Opens `SelectBrowserWindow` |
| Select file manager | :x: Missing | Opens `SelectFileManagerWindow` |
| Portable mode | :x: Missing | `Settings.ShouldUsePinyin` |

### 2.2 Theme Settings (`SettingsPaneTheme.xaml` - 803 lines)
**Avalonia: 66 lines (~8%)**

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
| Query box font | :x: Missing | `Settings.QueryBoxFont` |
| Query box font size | :x: Missing | `Settings.QueryBoxFontSize` |
| Result font | :x: Missing | `Settings.ResultFont` |
| Result font size | :x: Missing | `Settings.ResultItemFontSize` |
| Result sub font size | :x: Missing | `Settings.ResultSubItemFontSize` |
| **Appearance** |
| Color scheme | :white_check_mark: Done | Light/Dark/System |
| Animation speed | :x: Missing | `Settings.AnimationSpeed` |
| Use clock | :x: Missing | `Settings.UseDate` |
| Clock format | :x: Missing | `Settings.TimeFormat` |
| Date format | :x: Missing | `Settings.DateFormat` |
| Use glyph icons | :x: Missing | `Settings.UseGlyphIcons` |
| **Backdrop** |
| Use system backdrop | :x: Missing | `Settings.UseSystemBackdrop` |
| Backdrop type | :x: Missing | Mica/Acrylic/etc |
| **Icon Settings** |
| Icon theme | :x: Missing | `Settings.ColorScheme` |
| Double-click icon action | :x: Missing | `Settings.DoubleClickIconAction` |

### 2.3 Hotkey Settings (`SettingsPaneHotkey.xaml` - 463 lines)
**Avalonia: 26 lines (~6%)**

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
**Avalonia: 47 lines (~33%)**

| Feature | Status | Notes |
|---------|--------|-------|
| Plugin list | :white_check_mark: Done | Basic list view |
| Plugin icon | :white_check_mark: Done | Shows icon |
| Plugin name | :white_check_mark: Done | Shows name |
| Plugin enable/disable | :x: Missing | Toggle switch |
| Plugin details panel | :x: Missing | Description, author, website |
| Plugin settings UI | :x: Missing | `IPluginSettingProvider` integration |
| Action keywords editor | :x: Missing | Opens `ActionKeywords` dialog |
| Plugin priority | :x: Missing | Drag to reorder |

### 2.5 Plugin Store (`SettingsPanePluginStore.xaml` - 400 lines)
**Avalonia: 0 lines (0%)**

| Feature | Status | Notes |
|---------|--------|-------|
| Plugin search | :x: Missing | Search box |
| Plugin grid | :x: Missing | Grid of available plugins |
| Plugin card | :x: Missing | Icon, name, description, install button |
| Install plugin | :x: Missing | Download and install |
| Update plugin | :x: Missing | Update available indicator |
| Uninstall plugin | :x: Missing | Remove plugin |
| Plugin filter | :x: Missing | Filter by category |

### 2.6 Proxy Settings (`SettingsPaneProxy.xaml` - 80 lines)
**Avalonia: 47 lines (~59%)**

| Setting | Status | Notes |
|---------|--------|-------|
| Enable proxy | :white_check_mark: Done | Toggle |
| Proxy server | :white_check_mark: Done | Text input |
| Proxy port | :white_check_mark: Done | Number input |
| Proxy username | :x: Missing | Text input |
| Proxy password | :x: Missing | Password input |

### 2.7 About Settings (`SettingsPaneAbout.xaml` - 184 lines)
**Avalonia: 25 lines (~14%)**

| Feature | Status | Notes |
|---------|--------|-------|
| Version display | :white_check_mark: Done | Shows version |
| Check for updates | :x: Missing | Button + status |
| Homepage link | :x: Missing | HyperLink |
| Documentation link | :x: Missing | HyperLink |
| GitHub link | :x: Missing | HyperLink |
| Discord link | :x: Missing | HyperLink |
| Release notes | :x: Missing | Opens `ReleaseNotesWindow` |
| Open logs folder | :x: Missing | Button |
| Open settings folder | :x: Missing | Button |

**Settings Progress: 312/2609 lines (~12%)**

---

## 3. Custom Controls

| Control | WPF File | Status | Description |
|---------|----------|--------|-------------|
| Card | `Card.xaml.cs` | :x: Missing | Settings card with icon, title, subtitle |
| ExCard | `ExCard.xaml.cs` | :x: Missing | Expandable card with nested content |
| CardGroup | `CardGroup.xaml.cs` | :x: Missing | Groups cards with rounded corners |
| InfoBar | `InfoBar.xaml.cs` | :x: Missing | Information/warning banner |
| HyperLink | `HyperLink.xaml.cs` | :x: Missing | Clickable link control |
| HotkeyDisplay | `HotkeyDisplay.xaml.cs` | :x: Missing | Displays hotkey as key badges |
| InstalledPluginDisplay | `InstalledPluginDisplay.xaml.cs` | :x: Missing | Plugin info card |
| InstalledPluginDisplayKeyword | `InstalledPluginDisplayKeyword.xaml.cs` | :x: Missing | Keyword badge |
| InstalledPluginDisplayBottomData | `InstalledPluginDisplayBottomData.xaml.cs` | :x: Missing | Plugin metadata footer |

**Progress: 0/9 (0%)**

---

## 4. ViewModels

| ViewModel | WPF Lines | Avalonia Lines | Status | Notes |
|-----------|-----------|----------------|--------|-------|
| MainViewModel | 2292 | ~420 | :yellow_circle: 18% | Core query works, missing history/clipboard |
| ResultsViewModel | ~200 | ~150 | :white_check_mark: 75% | Core functionality done |
| ResultViewModel | ~300 | ~200 | :white_check_mark: 67% | Basic display done |
| SettingWindowViewModel | ~100 | ~50 | :yellow_circle: 50% | Navigation works |
| PluginViewModel | ~150 | ~30 | :yellow_circle: 20% | Basic list only |
| PluginStoreItemViewModel | ~100 | 0 | :x: 0% | Not started |
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

**Progress: ~25%**

---

## 5. Helpers

| Helper | Status | Description |
|--------|--------|-------------|
| HotKeyMapper | :white_check_mark: Done | Hotkey registration |
| ImageLoader | :white_check_mark: Done | Image caching/loading |
| FontLoader | :white_check_mark: Done | Font loading |
| GlobalHotkey | :white_check_mark: Done | In Infrastructure project |
| AutoStartup | :x: Missing | Windows startup registration |
| SingleInstance | :x: Missing | Prevent multiple instances |
| ErrorReporting | :x: Missing | Error logging/reporting |
| ExceptionHelper | :x: Missing | Exception formatting |
| SingletonWindowOpener | :x: Missing | Manage singleton windows |
| WallpaperPathRetrieval | :x: Missing | Get desktop wallpaper |
| WindowsMediaPlayerHelper | :x: Missing | Preview audio files |
| DataWebRequestFactory | :x: Missing | Web requests with proxy |
| SyntaxSugars | :x: Missing | Extension methods |

**Progress: 4/13 (31%)**

---

## 6. Converters

| Converter | Status | Description |
|-----------|--------|-------------|
| HighlightTextConverter | :yellow_circle: Stub | Returns plain text, needs highlighting |
| QuerySuggestionBoxConverter | :yellow_circle: Stub | Not implemented |
| BoolToVisibilityConverter | :white_check_mark: Done | Boolean to visibility |
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

**Progress: 3/14 (21%)**

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
| Hide on focus loss | :x: Missing | Deactivate handling |
| Position memory | :x: Missing | Remember last position |
| Multi-monitor | :x: Missing | Position on specific monitor |
| Topmost mode | :x: Missing | Always on top option |

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
| Plugin settings UI | :x: Missing | IPluginSettingProvider |
| Plugin install | :x: Missing | From store |
| Plugin update | :x: Missing | Check/apply updates |
| Python plugins | :x: Missing | Python path config |
| Node plugins | :x: Missing | Node path config |

**Progress: ~45%**

---

## 8. Theming

| Feature | Status | Notes |
|---------|--------|-------|
| Light/Dark/System | :white_check_mark: Done | Basic switching |
| Custom themes | :x: Missing | Load from .xaml files |
| Theme list | :x: Missing | Browse available themes |
| Theme preview | :x: Missing | Live preview in settings |
| Font customization | :x: Missing | Query/result fonts |
| Size customization | :x: Missing | Window/item sizes |
| Animation speed | :x: Missing | Transition speed |
| Backdrop effects | :x: Missing | Mica/Acrylic |
| Icon themes | :x: Missing | Glyph/image icons |

**Progress: ~15%**

---

## 9. Animations

| Animation | Status | Notes |
|-----------|--------|-------|
| Window show/hide | :x: Missing | Fade/slide animation |
| Result list | :x: Missing | Item entrance animation |
| Context menu | :x: Missing | Menu slide animation |
| Settings navigation | :x: Missing | Page transitions |
| Progress indicators | :x: Missing | Loading spinners |

**Progress: 0%**

---

## Priority Recommendations

### High Priority (Core UX)
1. [ ] **HighlightTextConverter** - Search match highlighting
2. [ ] **History cycling** - Up/Down arrow through history
3. [ ] **GeneralSettingsPage** - Essential settings (startup, behavior)
4. [ ] **Card/ExCard controls** - Required for all settings pages
5. [ ] **Hide on focus loss** - Expected behavior

### Medium Priority (Feature Completeness)
6. [ ] **HotkeySettingsPage** - All keyboard shortcuts
7. [ ] **ThemeSettingsPage** - Theme selection and customization
8. [ ] **Plugin settings UI** - IPluginSettingProvider integration
9. [ ] **Plugin Store** - Install/update plugins
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
│   │   └── HotkeyRecorderDialog.axaml
│   └── SettingPages/
│       ├── SettingsWindow.axaml
│       ├── GeneralSettingsPage.axaml (71 lines)
│       ├── ThemeSettingsPage.axaml (66 lines)
│       ├── HotkeySettingsPage.axaml (26 lines)
│       ├── PluginsSettingsPage.axaml (47 lines)
│       ├── ProxySettingsPage.axaml (47 lines)
│       └── AboutSettingsPage.axaml (25 lines)
├── ViewModel/
│   ├── MainViewModel.cs (~420 lines)
│   └── SettingPages/*.cs
├── Helper/ (4 files)
├── Converters/ (3 files)
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
