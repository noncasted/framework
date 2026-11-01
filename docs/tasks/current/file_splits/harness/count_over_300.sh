#!/bin/bash
# Files above 300 lines in the scope of the task: lines, path, longest first. Run from the root of a tree.
git ls-files client/core client/flow client/modules client/platforms \
  | grep -E '\.cs$' \
  | grep -vE '\.g\.cs$|/Generated/|/Tests/|Tests\.cs$|ContainerGenerator\.Verify/' \
  | while IFS= read -r f; do n=$(wc -l < "$f"); [ "$n" -gt 300 ] && echo "$n $f"; done \
  | sort -k1,1rn -k2
