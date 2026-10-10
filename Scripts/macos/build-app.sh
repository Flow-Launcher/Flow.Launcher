#!/usr/bin/env bash
# Builds "Flow Launcher.app" (self-contained, ad-hoc signed) into Output/macOS/.
#
# Usage: Scripts/macos/build-app.sh [osx-arm64|osx-x64]   (default: osx-arm64)
#
# codesign treats every regular file and directory in Contents/MacOS as nested code, so the publish
# output cannot simply live there. The .NET host resolves symlinks of the app assembly and uses the
# real directory as the app directory (runtime, deps.json, TPA), so the whole publish output lives in
# Contents/Resources and Contents/MacOS only holds the apphost plus a symlink to the app assembly.
# Constant.ProgramDirectory (where the app looks for Images/, Languages/, Plugins/, ...) is therefore
# Contents/Resources:
#   Contents/Info.plist                         from Scripts/macos/Info.plist
#   Contents/MacOS/Flow.Launcher.Avalonia       apphost (CFBundleExecutable)
#   Contents/MacOS/Flow.Launcher.Avalonia.dll -> ../Resources/Flow.Launcher.Avalonia.dll
#   Contents/Resources/                         publish output (runtime, assemblies, Images/, Languages/, ...)
#   Contents/Resources/Plugins/<plugin>/        bundled plugins built for net10.0 (Constant.PreinstalledDirectory)
#   Contents/Resources/AppIcon.icns             generated from Flow.Launcher/Images/app.png
set -euo pipefail

RID="${1:-osx-arm64}"
case "$RID" in
    osx-arm64|osx-x64) ;;
    *) echo "usage: $0 [osx-arm64|osx-x64]" >&2; exit 2 ;;
esac

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "error: must run on macOS (needs sips, iconutil, codesign)" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CONFIGURATION=Release
TFM=net10.0
EXECUTABLE=Flow.Launcher.Avalonia
OUT_DIR="$REPO_ROOT/Output/macOS"
WORK_DIR="$OUT_DIR/obj/$RID"
PUBLISH_DIR="$WORK_DIR/publish"
APP="$OUT_DIR/Flow Launcher.app"
# Where the plugin projects' CopyToAvaloniaOutput target drops their net10.0 output.
PLUGIN_BUILD_DIR="$REPO_ROOT/Output/Avalonia/$CONFIGURATION/$TFM/Plugins"

VERSION="$(sed -n 's/.*AssemblyInformationalVersion("\([^"]*\)").*/\1/p' "$REPO_ROOT/SolutionAssemblyInfo.cs")"
VERSION="${VERSION:-1.0.0}"
# CFBundleShortVersionString allows at most three integers; CI versions are four-part (x.y.z.build).
SHORT_VERSION="$(cut -d. -f1-3 <<<"$VERSION")"

is_macho() {
    file -b "$1" | grep -q 'Mach-O'
}

echo "==> Publishing $EXECUTABLE ($TFM, $RID, self-contained, $CONFIGURATION)"
rm -rf "$WORK_DIR"
# ReadyToRun precompiles IL so first window/settings open skips most JIT (~30-50% faster cold paths, ~+45 MB).
dotnet publish "$REPO_ROOT/Flow.Launcher.Avalonia/Flow.Launcher.Avalonia.csproj" \
    -f "$TFM" -r "$RID" --self-contained -c "$CONFIGURATION" -p:PublishReadyToRun=true -o "$PUBLISH_DIR" -nologo -v quiet -clp:NoSummary

echo "==> Building bundled plugins ($TFM, $CONFIGURATION)"
PLUGINS=()
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
    PLUGINS+=("$name")
done

echo "==> Assembling $APP"
rm -rf "$APP"
CONTENTS="$APP/Contents"
mkdir -p "$CONTENTS/MacOS" "$CONTENTS/Resources/Plugins"

cp -R "$PUBLISH_DIR/." "$CONTENTS/Resources/"
mv "$CONTENTS/Resources/$EXECUTABLE" "$CONTENTS/MacOS/$EXECUTABLE"
ln -s "../Resources/$EXECUTABLE.dll" "$CONTENTS/MacOS/$EXECUTABLE.dll"

for name in "${PLUGINS[@]}"; do
    dest="$CONTENTS/Resources/Plugins/$name"
    cp -R "$PLUGIN_BUILD_DIR/$name" "$dest"
    # Framework-dependent plugin builds carry native assets for every RID; keep only macOS/unix ones.
    if [[ -d "$dest/runtimes" ]]; then
        find "$dest/runtimes" -mindepth 1 -maxdepth 1 -type d ! -name 'osx*' ! -name 'unix' -exec rm -rf {} +
    fi
done

sed -e "s/@EXECUTABLE@/$EXECUTABLE/g" -e "s/@SHORT_VERSION@/$SHORT_VERSION/g" -e "s/@VERSION@/$VERSION/g" \
    "$SCRIPT_DIR/Info.plist" > "$CONTENTS/Info.plist"
plutil -lint "$CONTENTS/Info.plist" >/dev/null

echo "==> Generating AppIcon.icns"
ICONSET="$WORK_DIR/AppIcon.iconset"
ICON_SRC="$REPO_ROOT/Flow.Launcher/Images/app.png"
mkdir -p "$ICONSET"
# The source is 256x256; larger representations are not generated to avoid upscaling.
for size in 16 32 128 256; do
    sips -z "$size" "$size" "$ICON_SRC" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
done
for size in 16 32 128; do
    double=$((size * 2))
    sips -z "$double" "$double" "$ICON_SRC" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$CONTENTS/Resources/AppIcon.icns"

echo "==> Ad-hoc signing"
# The runtime, Avalonia and plugin native libraries sit in Contents/Resources where --deep does not reach;
# sign them first, then the bundle (apphost + resource seal).
while IFS= read -r -d '' file; do
    if is_macho "$file"; then
        codesign --force -s - "$file"
    fi
done < <(find "$CONTENTS/Resources" -type f -print0)
codesign --force --deep -s - "$APP"
codesign --verify --deep --strict "$APP"

echo "==> Done: $APP ($RID, version $VERSION)"
