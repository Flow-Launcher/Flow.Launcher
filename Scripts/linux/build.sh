#!/usr/bin/env bash
# Builds a self-contained Flow Launcher for Linux into Output/Linux/ and packs it as a tarball.
#
# Usage: Scripts/linux/build.sh [linux-x64|linux-arm64]   (default: linux-x64)
#
# Layout of Output/Linux/flow-launcher-<rid>/ (also the root of flow-launcher-<rid>.tar.gz):
#   Flow.Launcher.Avalonia            apphost; run it from anywhere, no install step
#   *.dll, Images/, Languages/, ...   publish output (Constant.ProgramDirectory)
#   Plugins/<plugin>/                 bundled plugins built for net10.0 (Constant.PreinstalledDirectory)
#
# On first start the app installs its desktop entry (~/.local/share/applications/com.flowlauncher.FlowLauncher.desktop)
# pointing at the apphost, so it shows up in application menus and can register for global shortcuts.
set -euo pipefail

RID="${1:-linux-x64}"
case "$RID" in
    linux-x64|linux-arm64) ;;
    *) echo "usage: $0 [linux-x64|linux-arm64]" >&2; exit 2 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CONFIGURATION=Release
TFM=net10.0
OUT_DIR="$REPO_ROOT/Output/Linux"
APP_DIR="$OUT_DIR/flow-launcher-$RID"
TARBALL="$OUT_DIR/flow-launcher-$RID.tar.gz"
# Where the plugin projects' CopyToAvaloniaOutput target drops their net10.0 output.
PLUGIN_BUILD_DIR="$REPO_ROOT/Output/Avalonia/$CONFIGURATION/$TFM/Plugins"

echo "==> Publishing Flow.Launcher.Avalonia ($TFM, $RID, self-contained, $CONFIGURATION)"
rm -rf "$APP_DIR" "$TARBALL"
# ReadyToRun precompiles IL so the first window/settings open skips most JIT.
dotnet publish "$REPO_ROOT/Flow.Launcher.Avalonia/Flow.Launcher.Avalonia.csproj" \
    -f "$TFM" -r "$RID" --self-contained -c "$CONFIGURATION" -p:PublishReadyToRun=true -o "$APP_DIR" -nologo -v quiet -clp:NoSummary

echo "==> Building bundled plugins ($TFM, $CONFIGURATION)"
mkdir -p "$APP_DIR/Plugins"
for plugin_dir in "$REPO_ROOT"/Plugins/*/; do
    name="$(basename "$plugin_dir")"
    csproj="$plugin_dir$name.csproj"
    [[ -f "$csproj" ]] || continue
    # Only plugins that multi-target the cross-platform TFM; Windows-only plugins are skipped.
    if ! grep -Eq "<TargetFrameworks>([^<]*;)?$TFM(;|<)" "$csproj"; then
        echo "    skipping $name (Windows-only)"
        continue
    fi
    echo "    $name"
    rm -rf "${PLUGIN_BUILD_DIR:?}/$name"
    dotnet build "$csproj" -f "$TFM" -c "$CONFIGURATION" -nologo -v quiet -clp:NoSummary
    if [[ ! -f "$PLUGIN_BUILD_DIR/$name/$name.dll" ]]; then
        echo "error: $name build did not produce $PLUGIN_BUILD_DIR/$name/$name.dll" >&2
        exit 1
    fi

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
