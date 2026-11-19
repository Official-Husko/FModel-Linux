#!/usr/bin/env bash
# Packages FModel as a single portable AppImage: runtime, native libraries and all, nothing to install.
#
#   linux/scripts/build-appimage.sh   -> linux/out/FModel-x86_64.AppImage
#
# Needs the same tools as build.sh, plus network access to fetch appimagetool on first use.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$ROOT/linux/out"
APPDIR="$OUT/FModel.AppDir"
TARGET="${APPIMAGE_OUTPUT:-$OUT/FModel-x86_64.AppImage}"
APPIMAGETOOL_VERSION="${APPIMAGETOOL_VERSION:-1.9.1}"

source "$ROOT/linux/scripts/prepare.sh"

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/256x256/apps"

dotnet publish "$ROOT/linux/FModel.Linux.csproj" -c Release -r linux-x64 --self-contained true -o "$APPDIR/usr/lib/fmodel" \
    -p:PublishSingleFile=false -p:PublishReadyToRun=false \
    -p:DebugType=None -p:DebugSymbols=false -p:GenerateDocumentationFile=false \
    ${UPSTREAM_COMMIT:+-p:UpstreamCommit=$UPSTREAM_COMMIT}

ln -s ../lib/fmodel/FModel "$APPDIR/usr/bin/FModel"
install -m644 "$ROOT/linux/packaging/fmodel.desktop" "$APPDIR/fmodel.desktop"
install -m644 "$ROOT/linux/packaging/fmodel.desktop" "$APPDIR/usr/share/applications/fmodel.desktop"
install -m644 "$ROOT/linux/packaging/fmodel.png" "$APPDIR/fmodel.png"
install -m644 "$ROOT/linux/packaging/fmodel.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/fmodel.png"
ln -s fmodel.png "$APPDIR/.DirIcon"

cat > "$APPDIR/AppRun" <<'APPRUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/lib/fmodel/FModel" "$@"
APPRUN
chmod +x "$APPDIR/AppRun"

TOOL="$OUT/appimagetool-$APPIMAGETOOL_VERSION"
if [ ! -x "$TOOL" ]; then
    curl -fsSL -o "$TOOL" "https://github.com/AppImage/appimagetool/releases/download/$APPIMAGETOOL_VERSION/appimagetool-x86_64.AppImage"
    chmod +x "$TOOL"
fi

# --appimage-extract-and-run: works on machines and CI runners without FUSE
ARCH=x86_64 "$TOOL" --appimage-extract-and-run --no-appstream "$APPDIR" "$TARGET"

echo
echo "Built $TARGET"
