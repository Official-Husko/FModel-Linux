#!/usr/bin/env bash
# Merges the latest official FModel (4sval/FModel) into the current branch, then checks the Linux build.
#
#   linux/scripts/sync-upstream.sh            merge upstream/dev, build, print the drift report
#   linux/scripts/sync-upstream.sh --no-build merge only (CI builds in a separate step)
#
# A merge conflict stops the script with the conflicting files listed; resolve, commit, and run it again.
# Upstream files are rarely touched on this side, so conflicts should be rare (see linux/README.md).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BRANCH="${UPSTREAM_BRANCH:-dev}"
BUILD=1
[ "${1:-}" = "--no-build" ] && BUILD=0
cd "$ROOT"

git remote get-url upstream >/dev/null 2>&1 || git remote add upstream https://github.com/4sval/FModel.git
git fetch --quiet upstream "$BRANCH"
target="$(git rev-parse "upstream/$BRANCH")"

if git merge-base --is-ancestor "$target" HEAD; then
    echo "Already in sync with upstream/$BRANCH ($(git rev-parse --short "$target"))."
    exit 0
fi

base="$(git merge-base HEAD "$target")"
count="$(git rev-list --count "$base..$target")"
version="$(git show "$target:FModel/FModel.csproj" | grep -oPm1 '(?<=<Version>)[^<]+' || echo '?')"
subject="chore(upstream): sync with official FModel $version ($(git rev-parse --short "$target"))"
body="Brings in $count upstream commit$([ "$count" = 1 ] || echo s) from 4sval/FModel@$BRANCH:

$(git log --no-merges --format='- %s' "$base..$target" | head -50)"

if ! git merge --no-ff --no-edit -m "$subject" -m "$body" "$target"; then
    echo
    echo "Merge conflicts in:" >&2
    git diff --name-only --diff-filter=U | sed 's/^/  /' >&2
    echo "Resolve them, commit, then run this script again." >&2
    exit 1
fi

git submodule update --init --recursive
"$ROOT/linux/scripts/apply-patches.sh"

if [ "$BUILD" = 1 ]; then
    dotnet build "$ROOT/linux/FModel.Linux.csproj" -c Release -nologo -v quiet -p:UpstreamCommit="$target"
fi

echo
"$ROOT/linux/scripts/drift.sh" || true
