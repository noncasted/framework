#!/usr/bin/env python3
"""Turns a raw memory snapshot of the Coolify hosts into a per-project table (markdown).

Input: a folder with `<host>.txt` (free, docker stats, labels, smaps of every process) and `<host>.cgroup.txt`
(name, memory.current, memory.swap.current, anon, file, inactive_file of every container cgroup), as written by
`snapshot.sh`. Output: markdown on stdout.

Columns, MiB:
- stats: what `docker stats` shows (memory.current - inactive_file);
- anon: private memory of the container in RAM (heaps, JIT code);
- swap: its pages pushed to swap (`docker stats` does not show them);
- cost: anon + swap, what the container really costs the host besides the shared page cache.
"""

import sys
from collections import defaultdict
from pathlib import Path

MIB = 1024 * 1024

DOTNET_ROLES = {"silo", "coordinator", "meta", "game", "console", "frontend", "resource-service", "backend"}


def read_sections(path):
    sections = defaultdict(list)
    current = "head"

    for line in path.read_text().splitlines():
        if line.startswith("#"):
            current = line[1:].split()[0]
            if current == "host":
                sections["host"].append(line)
            continue
        sections[current].append(line)

    return sections


def read_labels(lines):
    labels = {}

    for line in lines:
        parts = line.replace("\\t", "\t").split("\t")
        name = parts[0].lstrip("/")
        labels[name] = {"project": parts[2] or None, "service": parts[3] or None, "image": parts[5]}

    return labels


def read_cgroups(path):
    rows = {}

    for line in path.read_text().splitlines():
        parts = line.split("\t")
        name = parts[0].lstrip("/")
        current, swap, anon, file, inactive = (int(v) if v else 0 for v in parts[1:6])
        rows[name] = {"stats": current - inactive, "anon": anon, "swap": swap, "file": file}

    return rows


def kind_of(name, label):
    image = label["image"]
    role = name.split("-")[0] if "-" in name else name

    if name.startswith("aspire-dashboard"):
        return "aspire-dashboard"
    if name.startswith("coolify"):
        return "coolify"
    if "postgres" in image:
        return "postgres"
    if role in DOTNET_ROLES:
        return "dotnet:" + role
    if "pgbouncer" in image:
        return "pgbouncer"
    if "nginx" in image or role in ("web", "cdn"):
        return "web"
    return "other:" + role


def report(folder):
    out = []

    for host_file in sorted(folder.glob("*.txt")):
        if host_file.name.endswith(".cgroup.txt"):
            continue

        host = host_file.stem
        sections = read_sections(host_file)
        labels = read_labels(sections["labels"])
        cgroups = read_cgroups(folder / f"{host}.cgroup.txt")

        out.append(f"### {host}")
        out.append("")
        out.append("```")
        out.extend(sections["host"])
        out.extend(sections["head"][:3])
        out.append("```")
        out.append("")

        projects = defaultdict(lambda: defaultdict(lambda: {"stats": 0, "anon": 0, "swap": 0, "n": 0}))

        for name, cg in cgroups.items():
            label = labels.get(name, {"project": None, "service": None, "image": "?"})
            project = label["project"] or ("coolify" if name.startswith("coolify") else "(other)")
            kind = kind_of(name, label)
            group = "dotnet" if kind.startswith("dotnet:") else kind
            cell = projects[project][group]
            cell["stats"] += cg["stats"]
            cell["anon"] += cg["anon"]
            cell["swap"] += cg["swap"]
            cell["n"] += 1

        out.append("| project | part | containers | stats | anon | swap | cost |")
        out.append("|---|---|---|---|---|---|---|")
        host_total = {"stats": 0, "anon": 0, "swap": 0}

        for project in sorted(projects):
            total = {"stats": 0, "anon": 0, "swap": 0}

            for group in sorted(projects[project]):
                c = projects[project][group]
                out.append(f"| {project} | {group} | {c['n']} | {c['stats'] / MIB:.0f} | {c['anon'] / MIB:.0f} | "
                           f"{c['swap'] / MIB:.0f} | {(c['anon'] + c['swap']) / MIB:.0f} |")
                for key in total:
                    total[key] += c[key]

            out.append(f"| **{project}** | **total** | | **{total['stats'] / MIB:.0f}** | **{total['anon'] / MIB:.0f}** | "
                       f"**{total['swap'] / MIB:.0f}** | **{(total['anon'] + total['swap']) / MIB:.0f}** |")
            for key in host_total:
                host_total[key] += total[key]

        out.append(f"| **all** | | | **{host_total['stats'] / MIB:.0f}** | **{host_total['anon'] / MIB:.0f}** | "
                   f"**{host_total['swap'] / MIB:.0f}** | **{(host_total['anon'] + host_total['swap']) / MIB:.0f}** |")
        out.append("")

    return "\n".join(out)


if __name__ == "__main__":
    print(report(Path(sys.argv[1])))
