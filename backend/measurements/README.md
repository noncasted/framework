# Backend memory measurements

How much memory the deployed backends take on the Coolify hosts, before and after a change of the deploy (the
monolith of the cluster, the observability stack instead of the Aspire dashboard, GC settings). The numbers go into
[RESULTS.md](RESULTS.md) and into the release notes of the change.

```bash
backend/measurements/tools/snapshot.sh backend/measurements/results/<date>-<label> firstvds ovh   # read-only, over ssh
backend/measurements/tools/prod_report.py backend/measurements/results/<date>-<label>             # markdown tables
```

Hosts are ssh aliases of `~/.ssh/config`; a non-root user needs passwordless sudo (the smaps of the processes and the
cgroups of the containers). The snapshot only reads: `free`, `docker stats`, `docker inspect`, `/proc/<pid>/smaps_rollup`,
`/sys/fs/cgroup/.../memory.*`.

## What the columns mean (MiB)

| Column | Source | Meaning |
|---|---|---|
| stats | `memory.current - inactive_file` of the cgroup | what `docker stats` and Coolify show |
| anon | `anon` of `memory.stat` | private memory in RAM: GC heaps, JIT code, native allocations |
| swap | `memory.swap.current` | private pages pushed to swap; `docker stats` does not show them |
| cost | anon + swap | what the container costs the host besides the shared page cache; compare this one |

A project is grouped by the Coolify label `coolify.projectName`; `dotnet` is the sum of the roles (silo, coordinator,
meta, game, console, ...). Compare snapshots taken under the same conditions: a fresh deploy has not grown yet, a
container that ran for weeks has its idle pages in swap.

## Layout

| Path | What |
|---|---|
| `tools/snapshot.sh` | the read-only snapshot of the hosts |
| `tools/prod_report.py` | the tables of a snapshot folder |
| `results/<date>-<label>/` | raw snapshots: `<host>.txt` (free, stats, labels, smaps), `<host>.cgroup.txt` (cgroup memory) |
