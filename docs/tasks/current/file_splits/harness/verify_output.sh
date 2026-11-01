#!/bin/bash
# Output of the generator of a tree on every case of ContainerGenerator.Verify, plus the plain Verify run. Works on a
# temporary copy: client/core/Analyzers of the tree does not change, the sources of Verify get only VerifyCapture.cs.
# usage: verify_output.sh <tree> <empty output dir>
# Writes <output dir>/capture/ (one entry per compilation and per direct emitter call), MANIFEST.md5, verify.log
# (plain run), verify_capture.log. Prints the PASS counts and the total md5; exit code 1 when a case fails.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
tree="$(cd "$1" && pwd)"
mkdir -p "$2"
out="$(cd "$2" && pwd)"
[ -z "$(ls -A "$out")" ] || { echo "output dir is not empty: $out" >&2; exit 2; }
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/client/core/Source~" "$out/capture"
cp "$tree/Directory.Build.props" "$tree/Directory.Packages.props" "$tree/global.json" "$tmp/"
cp -r "$tree/client/core/Source~/ContainerGenerator" "$tree/client/core/Source~/ContainerGenerator.Verify" "$tmp/client/core/Source~/"
cp -r "$tree/client/core/Runtime" "$tmp/client/core/" # the audit cases compile the real runtime of the container
project="$tmp/client/core/Source~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj"
binary="$tmp/.build/bin/ContainerGenerator.Verify/Release/net10.0/ContainerGenerator.Verify"

dotnet build "$project" -c Release -v q > "$out/build_plain.log"
"$binary" > "$out/verify.log" || true
plain_pass=$(grep -c '^PASS' "$out/verify.log" || true)
plain_fail=$(grep -c '^FAIL' "$out/verify.log" || true)

cp "$here/verify_output/VerifyCapture.cs" "$tmp/client/core/Source~/ContainerGenerator.Verify/"
dotnet build "$project" -c Release -v q > "$out/build_capture.log"
VERIFY_CAPTURE_DIR="$out/capture" VERIFY_CAPTURE_TREE="$tmp" "$binary" > "$out/verify_capture.log" || true
capture_pass=$(grep -c '^PASS' "$out/verify_capture.log" || true)
capture_fail=$(grep -c '^FAIL' "$out/verify_capture.log" || true)

(cd "$out/capture" && find . -type f | LC_ALL=C sort | xargs -d '\n' md5sum) > "$out/MANIFEST.md5"
echo "verify: pass=$plain_pass fail=$plain_fail; with capture: pass=$capture_pass fail=$capture_fail; files=$(wc -l < "$out/MANIFEST.md5"); md5=$(md5sum < "$out/MANIFEST.md5" | cut -d' ' -f1)"
[ "$plain_fail" = 0 ] && [ "$capture_fail" = 0 ] && grep -q '^ALL PASSED' "$out/verify.log"
