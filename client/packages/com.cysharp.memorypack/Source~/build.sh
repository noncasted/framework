#!/usr/bin/env bash
# Builds the trimmed MemoryPack.Core and replaces Runtime/MemoryPack.Core.dll of this package.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
target="$root/../Runtime/MemoryPack.Core.dll"

dotnet build "$root/MemoryPack.Core.csproj" -c Release -o "$root/obj/out"
cp "$root/obj/out/MemoryPack.Core.dll" "$target"
echo "Copied to $target"
