#!/bin/bash
# Snapshot of the generator output. See gen_snapshot/Program.cs.
# usage: gen_snapshot.sh <ContainerGenerator.dll> <source root> <empty output dir> [project tree]
# The project tree (generated csproj, Library, references) defaults to the tree holding this script.
# Writes only to the output dir and to $TMPDIR/gen_snapshot (build of the harness).
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
tree="${4:-$(cd "$here/../../../../.." && pwd)}"
dotnet run -c Release --project "$here/gen_snapshot/gen_snapshot.csproj" -- "$1" "$2" "$3" "$tree"
