#!/usr/bin/env bash
# Shared first step of every Linux build: submodules at upstream's pinned commits, then the patch queue.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

git -C "$ROOT" submodule update --init --recursive
"$ROOT/linux/scripts/apply-patches.sh"

# The informational version records which official commit this build matches. CI passes it in; locally
# it is worked out from the upstream remote when there is one.
if [ -z "${UPSTREAM_COMMIT:-}" ] && git -C "$ROOT" rev-parse -q --verify upstream/dev >/dev/null; then
    UPSTREAM_COMMIT="$(git -C "$ROOT" merge-base HEAD upstream/dev)"
fi
export UPSTREAM_COMMIT="${UPSTREAM_COMMIT:-}"
