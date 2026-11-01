#!/bin/bash
# Builds ContainerGenerator.dll of a tree without touching client/core/Analyzers of that tree: the sources are copied to
# a temporary folder, its Release post-build target writes to the copy.
# usage: build_generator.sh <tree> <output dir>
set -euo pipefail
tree="$(cd "$1" && pwd)"
out="$2"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/client/core/Source~" "$out"
cp "$tree/Directory.Build.props" "$tree/Directory.Packages.props" "$tree/global.json" "$tmp/"
cp -r "$tree/client/core/Source~/ContainerGenerator" "$tmp/client/core/Source~/"
dotnet build "$tmp/client/core/Source~/ContainerGenerator/ContainerGenerator.csproj" -c Release -v q
cp "$tmp/.build/bin/ContainerGenerator/Release/netstandard2.0/ContainerGenerator.dll" "$out/"
