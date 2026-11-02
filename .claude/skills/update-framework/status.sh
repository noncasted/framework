#!/usr/bin/env bash
# Shows where the framework of this project stands and which releases an update to <target> goes through.
# Run from the root of the project, read from the target so that an old pin needs no copy of this script:
#   git -C tools/framework show <target>:.claude/skills/update-framework/status.sh | bash -s -- <target>
set -euo pipefail

target=${1:?usage: status.sh <tag or ref of the framework>}
fw=tools/framework

g() { git -C "$fw" "$@"; }

pin=$(git ls-tree HEAD "$fw" | awk '{print $3}')
[ -n "$pin" ] || { echo "error: no submodule at $fw" >&2; exit 1; }
g cat-file -e "$pin^{commit}" 2>/dev/null || { echo "error: pin $pin is not in $fw, fetch it" >&2; exit 1; }
g cat-file -e "$target^{commit}" 2>/dev/null || { echo "error: no ref $target in $fw, fetch it" >&2; exit 1; }

checkout=$(g rev-parse HEAD)
echo "pin:      ${pin:0:8} $(g describe --tags --always "$pin" 2>/dev/null)"
if [ "$checkout" = "$pin" ]; then
    echo "checkout: same as the pin"
else
    echo "checkout: ${checkout:0:8} DIFFERS from the pin"
fi
echo "dirty:    $(g status --short | wc -l) files in $fw"
echo "target:   $target = $(g rev-parse --short=8 "$target^{commit}"), $(g rev-list --count "$pin..$target") commits ahead of the pin"

# Commits of the pin the target does not have: `+` is really absent, `-` is there under another hash.
foreign=$(g cherry "$target" "$pin" | grep -c '^+' || true)
if [ "$foreign" != 0 ]; then
    echo "foreign:  $foreign commits of the pin have no identical patch in the target:"
    while read -r _ commit subject; do
        twin=$(g log --format=%h --fixed-strings --grep="$subject" "$target" | head -1)
        echo "          ${commit:0:8} $subject${twin:+ (same subject in the target: $twin)}"
    done < <(g cherry -v "$target" "$pin" | grep '^+')
fi

echo "pending:"
any=0
while read -r version; do
    [ -n "$version" ] || continue
    tag="v$version"
    g cat-file -e "$tag^{commit}" 2>/dev/null || { echo "error: release $version has no tag $tag" >&2; exit 1; }
    if g merge-base --is-ancestor "$tag" "$pin"; then
        continue
    fi
    any=1
    # No early exit from awk: with pipefail a closed pipe would end the script.
    base=$(g show "$target:releases/$version.md" | awk '/^base:/ && !found {found=$2} END {print found}')
    total=$(g rev-list --count "$base..$tag")
    missing=$(g rev-list --count "$tag" "^$base" "^$pin")
    if g merge-base --is-ancestor "$base" "$pin"; then
        echo "  $version: $((total - missing)) of $total commits are already in the pin"
    else
        echo "  $version: the pin is OLDER than its base ${base:0:8}: see its section for older pins"
    fi
done < <(g ls-tree --name-only "$target" releases/ | sed -n 's#^releases/\([0-9][0-9.]*\)\.md$#\1#p' | sort -V)
[ "$any" = 1 ] || echo "  none"

unreleased=$(g show "$target:releases/unreleased.md" | grep -c '^### ' || true)
if ! g describe --exact-match --tags "$target^{commit}" >/dev/null 2>&1 || [ "$unreleased" != 0 ]; then
    echo "unreleased: $unreleased steps in releases/unreleased.md of the target"
fi

if [ -e .claude/skills/update-framework ]; then
    echo "link:     .claude/skills/update-framework is there"
else
    echo "link:     .claude/skills/update-framework is MISSING"
fi
