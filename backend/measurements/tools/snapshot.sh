#!/usr/bin/env bash
# Read-only memory snapshot of Coolify hosts: free, docker stats, the labels of every container, smaps of every
# process and the cgroup memory (with swap) of every container. Writes <out>/<host>.txt and <out>/<host>.cgroup.txt;
# tools/prod_report.py turns the folder into tables.
#
#   backend/measurements/tools/snapshot.sh results/<date>-<label> firstvds ovh
#
# Hosts are ssh aliases (~/.ssh/config); a non-root user needs passwordless sudo for /proc and the cgroups.
set -euo pipefail

out=${1:?usage: snapshot.sh <out-folder> <ssh-host>...}
shift
[ $# -gt 0 ] || { echo "no hosts given" >&2; exit 2; }
mkdir -p "$out"

for host in "$@"; do
    ssh -o BatchMode=yes "$host" 'bash -s' > "$out/$host.txt" <<'REMOTE'
sudo -n true 2>/dev/null && S=sudo || S=
echo "#host $(hostname) $(date -u +%FT%TZ) nproc=$(nproc) uptime=$(cut -d" " -f1 /proc/uptime)"
free -m
echo "#df"
df -h / | tail -1
echo "#stats"
$S docker stats --no-stream --format "{{.Name}}\t{{.MemUsage}}\t{{.CPUPerc}}"
echo "#labels"
for c in $($S docker ps -q); do
    $S docker inspect -f '{{.Name}}\t{{index .Config.Labels "com.docker.compose.project"}}\t{{index .Config.Labels "coolify.projectName"}}\t{{index .Config.Labels "coolify.serviceName"}}\t{{index .Config.Labels "coolify.resourceName"}}\t{{.Config.Image}}\t{{.State.StartedAt}}\t{{.State.Pid}}' "$c"
done
echo "#pss"
for c in $($S docker ps -q); do
    n=$($S docker inspect -f '{{.Name}}' "$c")
    for pid in $($S docker top "$c" -eo pid 2>/dev/null | tail -n +2); do
        echo "$n $pid $($S awk '/^Rss:/{r=$2}/^Pss:/{p=$2}/^Anonymous:/{a=$2}END{print r,p,a}' /proc/$pid/smaps_rollup 2>/dev/null)"
    done
done
REMOTE

    ssh -o BatchMode=yes "$host" 'bash -s' > "$out/$host.cgroup.txt" <<'REMOTE'
sudo -n true 2>/dev/null && S=sudo || S=
for c in $($S docker ps -q --no-trunc); do
    n=$($S docker inspect -f '{{.Name}}' "$c")
    d=/sys/fs/cgroup/system.slice/docker-$c.scope
    [ -d "$d" ] || d=$(find /sys/fs/cgroup -maxdepth 4 -type d -name "*$c*" 2>/dev/null | head -1)
    stat() { $S awk -v k="$1" '$1 == k {print $2}' "$d/memory.stat"; }
    echo -e "$n\t$($S cat "$d/memory.current")\t$($S cat "$d/memory.swap.current")\t$(stat anon)\t$(stat file)\t$(stat inactive_file)"
done
REMOTE

    echo "$host: $(wc -l < "$out/$host.txt") + $(wc -l < "$out/$host.cgroup.txt") lines"
done
