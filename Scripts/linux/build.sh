#!/usr/bin/env bash
# Builds self-contained Flow Launcher release tarball(s) for Linux into Output/Linux/.
#
# Usage: Scripts/linux/build.sh [linux-x64|linux-arm64 ...]   (default: host architecture)
#
# Layout of Output/Linux/flow-launcher-<rid>/ (also the root of flow-launcher-<rid>.tar.gz):
#   Flow.Launcher.Avalonia            apphost; run it from anywhere, no install step
#   *.dll, Images/, Languages/, ...   publish output (Constant.ProgramDirectory)
#   Plugins/<plugin>/                 bundled plugins built for net10.0 (Constant.PreinstalledDirectory)
#
# On first start the app installs its desktop entry (~/.local/share/applications/com.flowlauncher.FlowLauncher.desktop)
# pointing at the apphost, so it shows up in application menus and can register for global shortcuts.
set -euo pipefail

RIDS=()
if [[ $# -eq 0 ]]; then
    case "$(uname -m)" in
        x86_64) RIDS=(linux-x64) ;;
        aarch64) RIDS=(linux-arm64) ;;
        *) echo "error: unsupported host architecture $(uname -m); specify linux-x64 or linux-arm64" >&2; exit 2 ;;
    esac
else
    for arg in "$@"; do
        case "$arg" in
            linux-x64|linux-arm64) RIDS+=("$arg") ;;
            *) echo "usage: $0 [linux-x64|linux-arm64 ...]" >&2; exit 2 ;;
        esac
    done
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CONFIGURATION=Release
TFM=net10.0
OUT_DIR="$REPO_ROOT/Output/Linux"
# Where the plugin projects' CopyToAvaloniaOutput target drops their net10.0 output.
PLUGIN_BUILD_DIR="$REPO_ROOT/Output/Avalonia/$CONFIGURATION/$TFM/Plugins"

echo "==> Building cross-platform projects and bundled plugins ($TFM, $CONFIGURATION)"
rm -rf "$PLUGIN_BUILD_DIR"
dotnet build "$REPO_ROOT/Scripts/Flow.Launcher.CrossPlatform.slnx" -c "$CONFIGURATION" -nologo -v quiet -clp:NoSummary

PLUGINS=()
for built_dir in "$PLUGIN_BUILD_DIR"/*/; do
    [[ -d "$built_dir" ]] || continue
    name="$(basename "$built_dir")"
    if [[ ! -f "$built_dir/$name.dll" ]]; then
        echo "error: $name build did not produce $built_dir/$name.dll" >&2
        exit 1
    fi
    PLUGINS+=("$name")
done
if [[ ${#PLUGINS[@]} -eq 0 ]]; then
    echo "error: no bundled plugins were built in $PLUGIN_BUILD_DIR" >&2
    exit 1
fi

for RID in "${RIDS[@]}"; do
    APP_DIR="$OUT_DIR/flow-launcher-$RID"
    TARBALL="$OUT_DIR/flow-launcher-$RID.tar.gz"

    echo "==> Publishing Flow.Launcher.Avalonia ($TFM, $RID, self-contained, $CONFIGURATION)"
    rm -rf "$APP_DIR" "$TARBALL"
    # ReadyToRun precompiles IL so the first window/settings open skips most JIT.
    dotnet publish "$REPO_ROOT/Flow.Launcher.Avalonia/Flow.Launcher.Avalonia.csproj" \
        -f "$TFM" -r "$RID" --self-contained -c "$CONFIGURATION" -p:PublishReadyToRun=true -o "$APP_DIR" -nologo -v quiet -clp:NoSummary

    mkdir -p "$APP_DIR/Plugins"
    for name in "${PLUGINS[@]}"; do
        dest="$APP_DIR/Plugins/$name"
        cp -R "$PLUGIN_BUILD_DIR/$name" "$dest"
        # Framework-dependent plugin builds carry native assets for every RID; keep only this one and the unix ones.
        if [[ -d "$dest/runtimes" ]]; then
            find "$dest/runtimes" -mindepth 1 -maxdepth 1 -type d ! -name "$RID" ! -name 'linux' ! -name 'unix' -exec rm -rf {} +
        fi
    done

    echo "==> Packing $TARBALL"
    tar -C "$OUT_DIR" -czf "$TARBALL" "flow-launcher-$RID"

    echo "==> Done: $APP_DIR ($RID)"
done
