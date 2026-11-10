# Flow Launcher on Linux

The Avalonia front end (`Flow.Launcher.Avalonia`) runs on Linux (`linux-x64`, `linux-arm64`) under X11 and under Wayland through XWayland. It needs an X server (XWayland is enough), a D-Bus session bus, and for Wayland hotkeys an `xdg-desktop-portal` with the GlobalShortcuts interface.

## Build

Requirements: the .NET 10 SDK (see `global.json`) and `tar`.

```sh
Scripts/linux/build.sh              # linux-x64 (default)
Scripts/linux/build.sh linux-arm64
```

The script:

1. publishes `Flow.Launcher.Avalonia` for `net10.0` as a self-contained ReadyToRun Release build for the given runtime into `Output/Linux/flow-launcher-<rid>/`;
2. builds every bundled plugin that targets `net10.0` into `Plugins/` (the Windows-only Explorer and WindowsSettings plugins are skipped). Native assets of other platforms are removed;
3. packs the folder as `Output/Linux/flow-launcher-<rid>.tar.gz`.

CI (`.github/workflows/dotnet.yml`, job `build-linux`) runs the script on Ubuntu for both runtimes and uploads the tarballs as the `Linux App (<rid>)` artifacts.

For development, the app also runs from the build output:

```sh
dotnet build Flow.Launcher.Avalonia -f net10.0
dotnet build Plugins/<plugin>/<plugin>.csproj -f net10.0   # bundled plugins land in Output/Avalonia/Debug/net10.0/Plugins
dotnet Output/Avalonia/Debug/net10.0/Flow.Launcher.Avalonia.dll
```

### NixOS

`flake.nix` provides a development shell with the .NET 10 SDK, `jj`, and the native libraries Avalonia loads at run time (X11, xcb, fontconfig, ICU, OpenGL):

```sh
nix develop
nix develop -c Scripts/linux/build.sh
nix develop -c dotnet Output/Avalonia/Debug/net10.0/Flow.Launcher.Avalonia.dll
```

Start built binaries from that shell (or with `nix-ld` providing the same libraries). A tarball built on NixOS uses the Nix store's dynamic loader; build on another distribution (or use the CI artifact) for a tarball that runs elsewhere.

## Install

Unpack the tarball anywhere and start `flow-launcher-<rid>/Flow.Launcher.Avalonia`. There is no install step: on start, Flow writes its desktop entry to `~/.local/share/applications/com.flowlauncher.FlowLauncher.desktop`, pointing at the running executable, unless a package already installed one. The entry puts Flow in application menus, gives its windows a name and icon, and is required to register for global shortcuts on Wayland. Flow keeps the generated entry up to date when the app moves; it never changes an entry without the `X-Flow-Launcher-Generated=true` key.

Flow Launcher runs as a tray app (StatusNotifierItem; on GNOME this needs the AppIndicator extension). Only one instance runs at a time: launching it again shows the search window of the running instance, and launching it with `--toggle` toggles the window.

User data (settings, logs, cache, plugins installed from the store) is stored in `$XDG_CONFIG_HOME/FlowLauncher` (`~/.config/FlowLauncher`).

**Start Flow Launcher on system startup** (Settings → General) writes the XDG autostart entry `~/.config/autostart/com.flowlauncher.FlowLauncher.desktop` for the running executable. Turning the setting off deletes it.

## Global hotkeys

Flow picks the backend from the session type:

| Session | Backend | Notes |
|---|---|---|
| X11 | X key grabs on the root window | Works like Windows. A combination another app has grabbed is reported as unavailable. |
| Wayland | `org.freedesktop.portal.GlobalShortcuts` | The compositor owns the keys. Flow binds one shortcut per hotkey; its id is the hotkey in lower case with `-` between keys (`Alt + Space` → `alt-space`). |
| Wayland without the portal | none | Registering a hotkey fails. Bind a desktop shortcut to `Flow.Launcher.Avalonia --toggle` instead. |

Desktops differ in how they assign the portal's shortcuts:

- **GNOME 48+ and KDE Plasma** ask for confirmation the first time and offer the configured hotkey as the suggested trigger. The shortcuts then appear in the desktop's keyboard settings.
- **Hyprland** (`xdg-desktop-portal-hyprland`) takes the key from its own configuration. Bind the shortcut id there, e.g. for the default hotkey:

  ```
  # hyprland.conf
  bind = ALT, SPACE, global, com.flowlauncher.FlowLauncher:alt-space
  ```

  ```lua
  -- hyprland.lua
  hl.bind("ALT + SPACE", hl.dsp.global("com.flowlauncher.FlowLauncher:alt-space"))
  ```

  `hyprctl globalshortcuts` lists the shortcuts Flow registered. When you change a hotkey in Flow, update the id in the binding.
- **Sway, niri and other wlroots compositors** without a GlobalShortcuts portal: bind `Flow.Launcher.Avalonia --toggle` (or `dotnet .../Flow.Launcher.Avalonia.dll --toggle` for development builds), e.g. `bindsym Mod1+space exec /path/to/Flow.Launcher.Avalonia --toggle`.

Custom query hotkeys (Settings → Hotkey) use the same backend, so on Hyprland each one needs its own binding.

## Windows and window managers

The search window is an X11 dialog-type window (`_NET_WM_WINDOW_TYPE_DIALOG`), so tiling window managers float and focus it, and Flow places it on the display chosen in Settings → General → **Search Window Location**. Under X11 the display with the mouse cursor is detected; under Wayland the cursor position is not available to XWayland apps, so that option falls back to the primary display. The "Focus" option (display of the active window) always falls back to the primary display. The windows' X11 class (`WM_CLASS`) is `com.flowlauncher.FlowLauncher`, for compositor window rules.

X11 has no blur-behind protocol shared by window managers, so blur themes (such as the default Win11Light) are drawn with an opaque background.

## Bundled plugins

- **Program** indexes desktop entries from `$XDG_DATA_HOME/applications` and every `$XDG_DATA_DIRS/applications` folder (including Flatpak and Nix profiles), honouring `NoDisplay`, `Hidden`, `OnlyShowIn`/`NotShowIn` and `TryExec`. Icons are resolved from the icon theme configured for GTK or KDE. Entries with `Terminal=true` start in `$TERMINAL` or the first terminal emulator found on `PATH`.
- **Shell** runs commands with `$SHELL` (else `/bin/sh`). "Leave shell open" and "close after key press" open a terminal emulator the same way.
- **Sys** shuts down, restarts, sleeps and hibernates through `systemctl` (or `loginctl` on elogind systems), logs out and locks through `loginctl`, and empties the freedesktop trash (`$XDG_DATA_HOME/Trash`).
- **Browser Bookmarks** reads Chrome, Chromium, Brave, Vivaldi, Edge and Firefox profiles from their Linux locations, including Flatpak and Snap installs.
- **Process Killer** lists the current user's processes and hides session-critical ones (compositor, display server, D-Bus, systemd).

## Known limitations

- The Explorer and WindowsSettings plugins are Windows-only and not bundled.
- Copying files to the clipboard (file drop) is not supported.
- Plugin settings panels that exist only in WPF are unavailable, as on macOS.
- Dialog Jump, portable mode, auto-update, the Backdrop type setting and the Korean IME option are unavailable. The `{clipboard}` and `{active_explorer_path}` custom shortcuts are unavailable.
- Python and Node.js runtimes for plugins are not installed automatically. Set the interpreter paths in Settings → General; the file picker there uses `zenity` or `kdialog`.
- Global keyboard hooks for plugins (key interception outside Flow's windows) are not available.
- "Ignore hotkeys in fullscreen mode" only detects fullscreen windows under X11.
