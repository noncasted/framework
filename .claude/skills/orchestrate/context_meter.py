#!/usr/bin/env python3
"""Report the context size of a Claude Code session or subagent from its transcript.

The context of a turn is input_tokens + cache_creation_input_tokens + cache_read_input_tokens
of the last assistant message in the transcript (.jsonl under ~/.claude/projects/<project>/).

Usage:
  context_meter.py --session <session-id>      main session (the id is in the scratchpad path)
  context_meter.py --agent <agent-id>          subagent by the id the Agent tool returned
  context_meter.py --marker <text>             subagent whose transcript contains the marker (its prompt carries it)
  context_meter.py ... --limit 300000          exit code 2 when the context is over the limit
"""
import argparse
import json
import os
import sys
from pathlib import Path

PROJECTS = Path.home() / ".claude" / "projects"


def last_context(path):
    context = None
    with open(path, encoding="utf-8") as f:
        for line in f:
            try:
                item = json.loads(line)
            except json.JSONDecodeError:
                continue
            message = item.get("message")
            if item.get("type") != "assistant" or not isinstance(message, dict):
                continue
            usage = message.get("usage")
            if not usage:
                continue
            context = (usage.get("input_tokens", 0)
                       + usage.get("cache_creation_input_tokens", 0)
                       + usage.get("cache_read_input_tokens", 0))
    return context


def transcripts(pattern):
    return sorted(PROJECTS.glob(pattern), key=os.path.getmtime, reverse=True)


def find_session(session_id):
    for path in transcripts("*/*.jsonl"):
        if path.name == f"{session_id}.jsonl":
            return path
    raise SystemExit(f"no transcript for session {session_id}")


def find_agent(agent_id):
    for path in transcripts(f"*/*/subagents/agent-{agent_id}.jsonl"):
        return path
    raise SystemExit(f"no transcript for agent {agent_id}")


def find_marker(marker):
    # The prompt of a subagent is in the parent transcript too, and the parent may itself be a subagent (an orchestrator
    # run as an agent): match only the first message of a transcript, which is the prompt of that subagent.
    for path in transcripts("*/*/subagents/*.jsonl"):
        with open(path, encoding="utf-8") as f:
            if marker in f.readline():
                return path
    raise SystemExit(f"no subagent transcript contains marker {marker!r}")


def main():
    parser = argparse.ArgumentParser()
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--session")
    group.add_argument("--agent")
    group.add_argument("--marker")
    parser.add_argument("--limit", type=int, default=300000)
    args = parser.parse_args()

    if args.session:
        path = find_session(args.session)
    elif args.agent:
        path = find_agent(args.agent)
    else:
        path = find_marker(args.marker)
    context = last_context(path)
    if context is None:
        raise SystemExit(f"no assistant usage in {path}")
    state = "OVER" if context > args.limit else "ok"
    print(f"{context} tokens ({context * 100 // args.limit}% of {args.limit}) {state} {path}")
    sys.exit(2 if context > args.limit else 0)


if __name__ == "__main__":
    main()
