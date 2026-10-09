# Flow Launcher on macOS

The Avalonia front end (`Flow.Launcher.Avalonia`) runs on macOS 13 or later, on Apple Silicon (`osx-arm64`) and Intel (`osx-x64`) Macs. Linux is not supported.

## Build

Requirements: macOS with the .NET 10 SDK (see `global.json`). The other tools the script uses (`codesign`, `iconutil`, `sips`, `plutil`) ship with macOS.

```sh
Scripts/macos/build-app.sh            # Apple Silicon (osx-arm64, default)
Scripts/macos/build-app.sh osx-x64    # Intel
```

The script:

1. publishes `Flow.Launcher.Avalonia` for `net10.0` as a self-contained Release build for the given runtime;
2. builds every bundled plugin that targets `net10.0` (the Windows-only Explorer and WindowsSettings plugins are skipped);
3. assembles `Output/macOS/Flow Launcher.app` with `Info.plist` from `Scripts/macos/Info.plist` and an icon generated from `Flow.Launcher/Images/app.png`;
4. signs the bundle ad hoc (`codesign -s -`) and verifies the signature.

Each run replaces `Output/macOS/Flow Launcher.app`, so building for the other architecture overwrites the previous bundle.

Bundle layout: `codesign` treats every file and folder in `Contents/MacOS` as code, so `Contents/MacOS` holds only the apphost (`Flow.Launcher.Avalonia`) and a symlink to `Flow.Launcher.Avalonia.dll`. The .NET host follows that symlink and uses `Contents/Resources` as the application directory. That folder holds the whole publish output (runtime, assemblies, `Images/`, `Languages/`) and the bundled plugins in `Contents/Resources/Plugins`. Native libraries there are signed one by one before the bundle is signed.

For development, the app also runs unbundled:

```sh
dotnet build Flow.Launcher.Avalonia -f net10.0
dotnet Output/Avalonia/Debug/net10.0/Flow.Launcher.Avalonia.dll
```

## Install

Copy `Output/macOS/Flow Launcher.app` to `/Applications`, then start it from Finder or with `open "/Applications/Flow Launcher.app"`.

Flow Launcher runs as a menu bar app. It has no Dock icon or app menu. Use the menu bar icon to open settings or quit. The default hotkey is Option+Space (the Windows `Alt` modifier maps to Option). You can change it in Settings → Hotkey.

User data (settings, logs, cache, plugins installed from the store) is stored in `~/Library/Application Support/FlowLauncher`.

## First run

- The bundle is signed ad hoc, not notarized. Builds you make yourself are not quarantined and open normally. If the app was downloaded or copied from another Mac, Gatekeeper blocks it. To open it anyway, right-click the app in Finder and choose **Open**, then confirm. On macOS 15 and later, you may instead need to allow it in System Settings → Privacy & Security ("Open Anyway"). Another option is to remove the quarantine flag with `xattr -dr com.apple.quarantine "/Applications/Flow Launcher.app"`.
- **Start Flow Launcher on system startup** (Settings → General) writes the per-user LaunchAgent `~/Library/LaunchAgents/com.flowlauncher.flowlauncher.plist`. The LaunchAgent starts the executable that is currently running, so turn the setting on after moving the app to its final location. macOS shows a "Background Items Added" notification when you turn it on. You can also manage the entry in System Settings → General → Login Items. Turning the setting off deletes the file. The "use logon task" option does nothing on macOS.
- Rebuilding changes the ad-hoc signature, so macOS may ask again for any privacy permissions that a plugin triggers (for example, access to files in protected folders).

## Known limitations

- The Explorer and WindowsSettings plugins are Windows-only and not bundled.
- Copying files to the clipboard (file drop) is not supported.
- Plugin settings panels that exist only in WPF are unavailable. Only plugins that provide Avalonia settings views show settings. Url and WebSearch have no settings panel on macOS.
- Dialog Jump, portable mode, Squirrel auto-update, and the theme blur/backdrop effects are unavailable. So is the Korean IME option.
- Python and Node.js runtimes for plugins are not installed automatically. Set the interpreter paths manually in Settings → General.
- The `{clipboard}` and `{active_explorer_path}` custom shortcuts are unavailable.
- ProcessKiller does not show window titles.
- Linux is not supported.
