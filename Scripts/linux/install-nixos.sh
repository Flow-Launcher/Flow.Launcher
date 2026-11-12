#!/usr/bin/env bash
# Installs Flow Launcher for the current user on NixOS (or any system with Nix), where published Linux builds
# cannot find the dynamic loader and native libraries they need.
#
# Usage: Scripts/linux/install-nixos.sh [prefix]   (default prefix: ~/.local)
#
# Result:
#   <prefix>/share/flow-launcher/app/       self-contained build (Scripts/linux/build.sh), replaced on every run
#   <prefix>/share/flow-launcher/runtime    the flake's runtime-libs (a GC root): loader and libraries
#   <prefix>/bin/flow-launcher              symlink to the app
#
# The binaries are patched to find everything in runtime/lib (like nixpkgs' autoPatchelfHook), so no
# LD_LIBRARY_PATH is needed and programs started from Flow do not inherit one. Quit Flow before re-running.
set -euo pipefail

PREFIX="${1:-$HOME/.local}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DEST="$PREFIX/share/flow-launcher"
LIBS="$DEST/runtime/lib"

case "$(uname -m)" in
    x86_64) RID=linux-x64; LOADER=ld-linux-x86-64.so.2 ;;
    aarch64) RID=linux-arm64; LOADER=ld-linux-aarch64.so.1 ;;
    *) echo "error: unsupported architecture $(uname -m)" >&2; exit 1 ;;
esac

if pgrep -f "$DEST/app/Flow.Launcher.Avalonia" >/dev/null; then
    echo "error: Flow Launcher is running from $DEST/app; quit it first" >&2
    exit 1
fi

nix develop "$REPO_ROOT" -c "$REPO_ROOT/Scripts/linux/build.sh" "$RID"

echo "==> Installing runtime libraries into $DEST/runtime"
mkdir -p "$DEST" "$PREFIX/bin"
nix build "$REPO_ROOT#runtime-libs" --out-link "$DEST/runtime"
PATCHELF="$(nix build nixpkgs#patchelf --no-link --print-out-paths)/bin/patchelf"

echo "==> Installing the app into $DEST/app"
rm -rf "$DEST/app.new"
cp -R "$REPO_ROOT/Output/Linux/flow-launcher-$RID" "$DEST/app.new"
# The apphost gets the runtime's loader; it and every native library search runtime/lib, which also covers
# libraries the .NET runtime dlopens on behalf of managed code (dlopen honours the caller's RUNPATH).
"$PATCHELF" --set-interpreter "$LIBS/$LOADER" --set-rpath "$LIBS" "$DEST/app.new/Flow.Launcher.Avalonia"
find "$DEST/app.new" -type f -name '*.so*' -print0 |
    while IFS= read -r -d '' library; do
        "$PATCHELF" --add-rpath "$LIBS" "$library"
    done
rm -rf "$DEST/app.old"
if [[ -d "$DEST/app" ]]; then mv "$DEST/app" "$DEST/app.old"; fi
mv "$DEST/app.new" "$DEST/app"
rm -rf "$DEST/app.old"

ln -sfn "$DEST/app/Flow.Launcher.Avalonia" "$PREFIX/bin/flow-launcher"

echo "==> Done: start Flow Launcher with $PREFIX/bin/flow-launcher"
