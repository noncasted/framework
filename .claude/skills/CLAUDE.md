# Shared skills

A game links each shared skill it uses: `ln -s ../../tools/framework/.claude/skills/<name> .claude/skills/<name>` (one link
per skill: a folder of skills nested under the project would load late and under a prefixed name). The name of the skill
and its `.claude/skills/<name>/...` paths stay as they are; its own skills sit next to the links. Edit a skill at its target
here, the tools do not write through a link. `update-framework` lists the shared skills a game has not linked (`status.sh`, `skills`)
and offers them; a refused one is written in `.claude/project-skills.md` § update-framework as `` Not linked: `<name>` ``. No values of a game in a skill: `.claude/project-skills.md` (targets, urls,
look), `.claude/code-audit.json` (folders of the dependency check, namespaces of the game), `.claude/control.json` (cluster, editor), `.claude/deploy-client.json` (targets of the client deploy), `.claude/override-packages` (registry and
patches); `${CLAUDE_PROJECT_DIR}` for absolute paths.

Cheap agents run in Claude Code on OpenCode Go: the profile `~/.claude-opencode` (the `clo` alias), never the profile of the
Anthropic subscription. In that profile every subagent runs on the model and effort of the session: a hook of the profile
drops the `model` / `effort` of an Agent call, so the per-step models of `orchestrate` apply only in the Anthropic profile. The subagents of the skill `audit-swarm` (`audit-scout`, `audit-verifier`, on the model of the session)
live in `.claude/skills/audit-swarm/agents`; its `install.sh`, run once per machine, links them into the user agents of that profile
(`~/.claude-opencode/agents`), so no project keeps an agents folder for them and the profile of the subscription never sees them.
