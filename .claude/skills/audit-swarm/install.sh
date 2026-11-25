#!/bin/bash
# Make the subagents of audit-swarm visible to Claude Code in the OpenCode Go profile (the `clo` alias) in every project:
# link them into the user agent folder of that profile. The agents run on the model of the session and are meant for the
# cheap models of OpenCode Go, so they live only in that profile (another profile: CLAUDE_CONFIG_DIR=<dir> install.sh). The sources stay next to the skill, no
# project keeps an agents folder for them.
set -euo pipefail

here=$(dirname "$(readlink -f "$0")")
profile="${CLAUDE_CONFIG_DIR:-$HOME/.claude-opencode}"
agents="$profile/agents"

[[ -f "$profile/settings.json" ]] || { echo "install.sh: no Claude Code profile $profile (settings.json)" >&2; exit 1; }

mkdir -p "$agents"
for file in "$here"/agents/*.md; do
  ln -sfn "$file" "$agents/$(basename "$file")"
  echo "$agents/$(basename "$file") -> $file"
done
