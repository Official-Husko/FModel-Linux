#!/usr/bin/env bash
# Installs FModel for the current user from an extracted FModel-linux-*.tar.gz.
#   ./install.sh                 installs to ~/.local
#   PREFIX=/opt/fmodel ./install.sh
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PREFIX="${PREFIX:-$HOME/.local}"
LIB="$PREFIX/lib/fmodel"

mkdir -p "$LIB" "$PREFIX/bin" "$PREFIX/share/applications" "$PREFIX/share/icons/hicolor/256x256/apps"
cp -a "$HERE/." "$LIB/"
ln -sf "$LIB/FModel" "$PREFIX/bin/FModel"
install -m644 "$HERE/fmodel.png" "$PREFIX/share/icons/hicolor/256x256/apps/fmodel.png"
sed "s|^Exec=.*|Exec=$PREFIX/bin/FModel|" "$HERE/fmodel.desktop" > "$PREFIX/share/applications/fmodel.desktop"
command -v update-desktop-database >/dev/null && update-desktop-database "$PREFIX/share/applications" || true

echo "FModel installed to $LIB (run it with: $PREFIX/bin/FModel)"
echo "Settings live in ~/.config/FModel. Exports go to ~/.local/share/FModel/Output unless you pick another folder."
