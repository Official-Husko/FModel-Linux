#!/usr/bin/env bash
# Applies the Linux patch queue to the submodules.
#
# CUE4Parse and Snooper are upstream's submodules and are never forked here. When one of them needs a
# Linux fix before it lands upstream, the fix lives in linux/patches/<Submodule>/NNNN-description.patch and
# this script applies it on top of whatever commit upstream pins. Already applied patches are skipped,
# so it is safe to run on every build. A patch that no longer applies (because upstream changed that
# code, or merged the fix) fails the build loudly: drop or refresh it.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
status=0

for dir in "$ROOT"/linux/patches/*/; do
    submodule="$(basename "$dir")"
    for patch in "$dir"*.patch; do
        [ -e "$patch" ] || continue
        name="$submodule/$(basename "$patch")"
        if git -C "$ROOT/$submodule" apply --reverse --check "$patch" 2>/dev/null; then
            echo "  already applied  $name"
        elif git -C "$ROOT/$submodule" apply --3way "$patch"; then
            echo "  applied          $name"
        else
            echo "  FAILED           $name (refresh or remove it)" >&2
            status=1
        fi
    done
done

exit $status
