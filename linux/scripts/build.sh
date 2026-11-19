#!/usr/bin/env bash
# Builds a self-contained, native Linux release of FModel (no .NET install needed to run it).
#
#   linux/scripts/build.sh            -> linux/out/FModel-linux-x64/ and FModel-linux-x64.tar.gz
#
# Needs: .NET 10 SDK, cmake, a C/C++ compiler (CUE4Parse's native library and libwin32compat).
set -euo pipefail

RID="${1:-linux-x64}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$ROOT/linux/out"
DEST="$OUT/FModel-$RID"

if [ "$RID" != "linux-$(uname -m | sed 's/x86_64/x64/;s/aarch64/arm64/')" ]; then
    echo "warning: the native libraries are compiled for this machine ($(uname -m)), build $RID on a matching host" >&2
fi

source "$ROOT/linux/scripts/prepare.sh"

rm -rf "$DEST"
# Not a single-file bundle on purpose: FModel reads its version from the FModel.dll file on disk.
dotnet publish "$ROOT/linux/FModel.Linux.csproj" -c Release -r "$RID" --self-contained true -o "$DEST" \
    -p:PublishSingleFile=false -p:PublishReadyToRun=false \
    -p:DebugType=None -p:DebugSymbols=false -p:GenerateDocumentationFile=false \
    ${UPSTREAM_COMMIT:+-p:UpstreamCommit=$UPSTREAM_COMMIT}

cp "$ROOT/linux/packaging/fmodel.desktop" "$ROOT/linux/packaging/fmodel.png" "$ROOT/linux/packaging/install.sh" "$DEST/"
tar -C "$OUT" -czf "$OUT/FModel-$RID.tar.gz" "FModel-$RID"

echo
echo "Built $DEST"
echo "      $OUT/FModel-$RID.tar.gz"
