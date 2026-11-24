#!/usr/bin/env bash
# Builds the trimmed MemoryPack.Core and replaces Runtime/MemoryPack.Core.dll of this package.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
target="$root/../Runtime/MemoryPack.Core.dll"

# A standalone build: the Directory.Build.props / Directory.Packages.props of the framework (shared obj folder, central
# package versions) are not for this project.
dotnet build "$root/MemoryPack.Core.csproj" -c Release -o "$root/obj/out" -p:ImportDirectoryBuildProps=false -p:ImportDirectoryPackagesProps=false
cp "$root/obj/out/MemoryPack.Core.dll" "$target"
echo "Copied to $target"
