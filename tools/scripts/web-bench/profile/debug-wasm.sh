#!/usr/bin/env bash
# Swaps the wasm of a Unity WebGL build for the debug wasm of the same build, so a CPU profile of the page shows the
# function names instead of wasm-function[N]. framework.js stays as it is: both wasm files come from one link.
#
#   debug-wasm.sh <build-dir> <unity-project>            replace Build/*.wasm.br, the original kept as *.wasm.br.orig
#   debug-wasm.sh --restore <build-dir>                  put the original back
#
# <build-dir>      the built player: its Build/ holds exactly one *.wasm.br (brotli compression of the build)
# <unity-project>  the Unity project that made the build (or the root of a game whose client/ is it): its
#                  Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.wasm is compressed with brotli -q 4
# --force          take a debug wasm whose time is more than 10 minutes away from the build (another build of the copy)
#
# Example:
#   debug-wasm.sh /tmp/claude-1000/s43/named /var/tmp/ps-prof43
#   debug-wasm.sh --restore /tmp/claude-1000/s43/named
set -euo pipefail

usage() { sed -n '2,/^set -/p' "$0" | sed '$d' | sed 's/^# \{0,1\}//'; }
fail() { echo "debug-wasm.sh: $*" >&2; exit 1; }

restore=0 force=0 args=()
for a in "$@"; do
  case "$a" in
    -h|--help) usage; exit 0 ;;
    --restore) restore=1 ;;
    --force) force=1 ;;
    -*) fail "unknown option $a (see --help)" ;;
    *) args+=("$a") ;;
  esac
done

if (( restore )); then
  (( ${#args[@]} == 1 )) || fail "--restore takes <build-dir> only (see --help)"
else
  (( ${#args[@]} == 2 )) || fail "expected <build-dir> <unity-project> (see --help)"
fi

build="${args[0]}"
[[ -d "$build/Build" ]] || fail "$build/Build: no such folder, not a WebGL build"

if (( restore )); then
  shopt -s nullglob
  origs=("$build"/Build/*.wasm.br.orig)
  (( ${#origs[@]} == 1 )) || fail "$build/Build: expected one *.wasm.br.orig, found ${#origs[@]}"
  mv -f "${origs[0]}" "${origs[0]%.orig}"
  echo "restored ${origs[0]%.orig} $(md5sum < "${origs[0]%.orig}" | cut -d' ' -f1)"
  exit 0
fi

command -v brotli >/dev/null || fail "brotli is not installed"

project="${args[1]}"
rel=Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.wasm
if [[ -f "$project/$rel" ]]; then
  source="$project/$rel"
elif [[ -f "$project/client/$rel" ]]; then
  source="$project/client/$rel"
else
  fail "no debug wasm: neither $project/$rel nor $project/client/$rel"
fi

shopt -s nullglob
wasms=("$build"/Build/*.wasm.br)
(( ${#wasms[@]} == 1 )) || fail "$build/Build: expected exactly one *.wasm.br, found ${#wasms[@]}"
target="${wasms[0]}"
[[ ! -e "$target.orig" ]] || fail "$target.orig exists: the build is already swapped, run --restore first"

gap=$(( $(stat -c %Y "$source") - $(stat -c %Y "$target") ))
if (( ${gap#-} > 600 && ! force )); then
  fail "$source is ${gap#-} s away from $target: probably another build; rebuild, or --force"
fi

tmp="$target.tmp.$$"
trap 'rm -f "$tmp"' EXIT
brotli -q 4 -c "$source" > "$tmp"
mv "$target" "$target.orig"
mv "$tmp" "$target"
echo "swapped $target $(md5sum < "$target" | cut -d' ' -f1) <- $source ($(stat -c %s "$source") bytes), original in $target.orig"
