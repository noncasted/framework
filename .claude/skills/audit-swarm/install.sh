#!/bin/bash
# Make the subagents of audit-swarm visible to oh-my-pi (omp) in every project: link them into the user agent folder of omp
# (~/.omp/agent/agents) and set the task limits in its global config. omp reads agents only from .omp/agents, .claude/agents
# (Claude Code reads it too) and the user folder, so the sources stay next to the skill and no project keeps a .omp folder.
set -euo pipefail

here=$(dirname "$(readlink -f "$0")")
agents="$HOME/.omp/agent/agents"

command -v omp >/dev/null || { echo "install.sh: omp not found" >&2; exit 1; }

mkdir -p "$agents"
for file in "$here"/agents/*.md; do
  ln -sfn "$file" "$agents/$(basename "$file")"
  echo "$agents/$(basename "$file") -> $file"
done

# A subagent that loops (a fast model over read calls) ends after 40 minutes instead of holding the run.
omp config set task.maxRuntimeMs 2400000
omp config set task.maxConcurrency 12
