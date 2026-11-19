#!/usr/bin/env bash
# Reports what changed upstream that the Linux build has to mirror by hand.
#
#   linux/scripts/drift.sh                 compare linux/UPSTREAM_PORTED with the merged upstream commit
#   linux/scripts/drift.sh <from> [<to>]   compare two upstream commits
#
# Almost all of upstream compiles on Linux unchanged. What doesn't is listed below, and that is exactly
# what this script watches:
#   - WPF views (*.xaml / *.xaml.cs), mirrored as Avalonia views in linux/src/FModel
#   - upstream files replaced by a file of the same path in linux/src/FModel
#   - upstream files left out of the build in linux/FModel.Linux.csproj
#   - NuGet packages added, removed or bumped in FModel/FModel.csproj
#   - new P/Invokes into Windows libraries
#
# When everything has been ported, write the <to> commit into linux/UPSTREAM_PORTED.
# Prints Markdown (used as-is for CI issues and release notes). Exit code 0 = nothing to do, 2 = work to do.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

FROM="${1:-$(tr -d '[:space:]' < linux/UPSTREAM_PORTED)}"
if [ -n "${2:-}" ]; then
    TO="$2"
elif git rev-parse -q --verify upstream/dev >/dev/null; then
    TO="$(git merge-base HEAD upstream/dev)"
else
    TO="HEAD"
fi

short() { git rev-parse --short "$1"; }
overlay="linux/src/FModel"
csproj="linux/FModel.Linux.csproj"

declare -a views=() overrides=() excluded=() missing=() pinvokes=()

while IFS= read -r file; do
    rel="${file#FModel/}"
    case "$file" in
        FModel/App.xaml.cs|FModel/Views/Resources/Icons.xaml|FModel/Views/Resources/Colors.xaml)
            ;; # compiled as is, or generated from upstream at build time
        *.xaml)
            if [ -f "$overlay/${rel%.xaml}.axaml" ]; then views+=("$file"); else missing+=("$file"); fi ;;
        *.xaml.cs)
            [ -f "$overlay/${rel%.xaml.cs}.axaml.cs" ] && views+=("$file") || missing+=("$file") ;;
        *.cs)
            if [ -f "$overlay/$rel" ]; then
                overrides+=("$file")
            elif grep -q "Remove=\"\$(UpstreamDir)${rel//\//\\\\}\"" "$csproj"; then
                excluded+=("$file")
            fi ;;
    esac
done < <(git diff --name-only "$FROM" "$TO" -- FModel/)

while IFS= read -r line; do
    pinvokes+=("$line")
done < <(git diff "$FROM" "$TO" -- FModel CUE4Parse Snooper | grep -E '^\+.*(DllImport|LibraryImport)\("(kernel32|user32|gdi32|shell32|advapi32|ole32|dwmapi|winbrand|ntdll)' || true)

packages="$(diff <(git show "$FROM:FModel/FModel.csproj" | grep -o '<PackageReference Include="[^"]*" Version="[^"]*"' | sort) \
                 <(git show "$TO:FModel/FModel.csproj"   | grep -o '<PackageReference Include="[^"]*" Version="[^"]*"' | sort) | grep '^[<>]' || true)"

version_from="$(git show "$FROM:FModel/FModel.csproj" | grep -oPm1 '(?<=<Version>)[^<]+' || true)"
version_to="$(git show "$TO:FModel/FModel.csproj" | grep -oPm1 '(?<=<Version>)[^<]+' || true)"

list() {
    local title="$1" hint="$2"; shift 2
    [ "$#" -gt 0 ] || return 0
    echo "### $title"
    echo
    echo "$hint"
    echo
    for f in "$@"; do echo "- \`$f\` ([diff](https://github.com/4sval/FModel/compare/$(short "$FROM")...$(short "$TO")#diff-$(printf '%s' "$f" | sha256sum | cut -c1-64)))"; done
    echo
}

work=$(( ${#views[@]} + ${#overrides[@]} + ${#excluded[@]} + ${#missing[@]} + ${#pinvokes[@]} ))
[ -n "$packages" ] && work=$((work + 1))

echo "## Upstream drift $(short "$FROM")..$(short "$TO")"
echo
echo "$(git rev-list --count "$FROM..$TO") upstream commits, FModel ${version_from:-?} -> ${version_to:-?}."
echo "Everything not listed here compiles on Linux unchanged."
echo

if [ "$work" -eq 0 ]; then
    echo "Nothing to port. Bump \`linux/UPSTREAM_PORTED\` to \`$(git rev-parse "$TO")\`."
    exit 0
fi

list "Views to update" "Upstream changed these WPF views; apply the same change to the Avalonia version in \`$overlay\`." "${views[@]}"
list "New views" "Upstream added views that have no Avalonia version yet." "${missing[@]}"
list "Replaced files to update" "These upstream files are replaced on Linux by the file with the same path in \`$overlay\`; port the change." "${overrides[@]}"
list "Excluded files that changed" "Left out of the Linux build in \`$csproj\`; check whether the change matters on Linux." "${excluded[@]}"

if [ -n "$packages" ]; then
    echo "### Package changes"
    echo
    echo "Mirror these in \`$csproj\` (\`<\` removed, \`>\` added):"
    echo
    echo '```'
    echo "$packages"
    echo '```'
    echo
fi

if [ "${#pinvokes[@]}" -gt 0 ]; then
    echo "### New Windows P/Invokes"
    echo
    echo "Add these functions to \`linux/native/win32compat.c\` or they throw on Linux:"
    echo
    echo '```'
    printf '%s\n' "${pinvokes[@]}"
    echo '```'
    echo
fi

exit 2
