#!/bin/bash
# The whole check of a generator change: builds the generator of a tree, compares the snapshot and the output on the
# Verify cases with the baselines taken before the split. Touches only <empty output dir> and temporary folders.
# usage: check_generator.sh <tree> <empty output dir>
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
tree="$(cd "$1" && pwd)"
mkdir -p "$2"
out="$(cd "$2" && pwd)"
"$here/build_generator.sh" "$tree" "$out/dll" > "$out/build.log" 2>&1 || { tail -30 "$out/build.log"; exit 1; }
grep -E 'warning|error' "$out/build.log" | head -20 || true
"$here/gen_snapshot.sh" "$out/dll/ContainerGenerator.dll" "$tree" "$out/snapshot" "$tree" > "$out/snapshot.log" 2>&1
"$here/verify_output.sh" "$tree" "$out/verify"
status=0
if diff -q "$here/baseline_manifest.md5" "$out/snapshot/MANIFEST.md5" > /dev/null; then
  echo "snapshot: same as the baseline, $(tail -1 "$out/snapshot/MANIFEST.md5")"
else
  echo "snapshot: DIFFERS"; diff "$here/baseline_manifest.md5" "$out/snapshot/MANIFEST.md5" | head -20; status=1
fi
if grep -qv 'compileErrors=0' <(grep compileErrors "$out/snapshot/_compile_summary.txt"); then
  echo "snapshot: compile errors"; grep -v 'compileErrors=0' "$out/snapshot/_compile_summary.txt" | head; status=1
fi
if diff -q "$here/verify_output_baseline.md5" "$out/verify/MANIFEST.md5" > /dev/null; then
  echo "verify output: same as the baseline"
else
  echo "verify output: DIFFERS"; diff "$here/verify_output_baseline.md5" "$out/verify/MANIFEST.md5" | head -20; status=1
fi
exit $status
