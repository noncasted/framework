#!/bin/bash
# Output of a generator on a game, read only. See game_snapshot/Program.cs.
# usage: game_snapshot.sh <ContainerGenerator.dll> <client dir of the game> <empty output dir>
# Writes only to the output dir and to $TMPDIR/game_snapshot (build of the harness).
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dotnet run -c Release --project "$here/game_snapshot/game_snapshot.csproj" -- "$1" "$2" "$3"
