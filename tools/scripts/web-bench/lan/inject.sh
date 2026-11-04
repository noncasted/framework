#!/usr/bin/env bash
# Puts lan/remote-log.js into a COPY of a WebGL build: copies the script next to index.html and loads it first in
# <head> (before the scripts of the template, so their errors are logged too). The template of the game stays as it is.
#
# Usage: inject.sh <build-dir>
#   <build-dir>  a copy of a build with index.html (never the build of the game itself: copy it first)
# A second run only refreshes remote-log.js. Fails when index.html has no <head> or the tag did not land.
# Example:
#   rm -rf /tmp/bench/site && cp -r <game>/builds/website /tmp/bench/site
#   tools/scripts/web-bench/lan/inject.sh /tmp/bench/site && tools/scripts/web-bench/lan/serve.py /tmp/bench/site
set -euo pipefail

if [[ $# -ne 1 || $1 == -h || $1 == --help ]]; then
    sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'
    [[ $# -eq 1 ]] && exit 0 || exit 2
fi

dir=$1
index=$dir/index.html
here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
tag='<script src="remote-log.js"></script>'

[[ -f $index ]] || { echo "inject.sh: no index.html in $dir" >&2; exit 1; }
cp "$here/remote-log.js" "$dir/remote-log.js"

if grep -qF "$tag" "$index"; then
    echo "remote-log.js already loaded by $index, the script refreshed"
    exit 0
fi
grep -qi '<head[^>]*>' "$index" || { echo "inject.sh: no <head> in $index" >&2; exit 1; }
sed -i "0,/<head[^>]*>/I s##&\n    $tag#" "$index"
grep -qF "$tag" "$index" || { echo "inject.sh: the tag did not land in $index" >&2; exit 1; }
echo "remote-log.js injected into $index"
