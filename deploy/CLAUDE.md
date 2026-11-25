# deploy

`registry.json` is the map of every deployment of the games: which VPS hosts which project, the Coolify uuids, the public
domains, the deploy branch. Sessions and scripts (logs, deploy, `claude-coolify`) read it to know where to go. No secrets.

## Schema

- `servers.<key>`: `ovh` (Poland) and `firstvds` (Kazakhstan); the same key names the MCP server (`coolify-<key>`), the ssh
  alias and `backend/measurements`. Fields: country, city, provider, ip, mcp, ssh, ssh_user, docker (the command: ovh needs
  `sudo -n docker`), panel (Coolify URL), `observability` (`logs` / `traces` read URLs on 127.0.0.1 of the host, `access:
  ssh-tunnel`: read through `ssh -L`, no tokens).
- `projects.<repository name>`: `framework` (false for post-radio, storytale, nikita-botik), `server`, `mode` (`services` |
  `monolith`), `namespace` (`service.namespace` of logs, = the key), `coolify` (project uuid, name, environment),
  `repository`, `branch`, `resources` (uuid, type application / service / database, role), `domains` by role, optional `note`.
- `neon-dodge` has no deploy: it is listed with `"deployed": false` and no server.
- `storytale` is v1: never redeploy or touch it.

## Reading

A game submodule lags behind main, so scripts read the latest main, not the working tree:
`git -C <framework> fetch -q origin main && git show origin/main:deploy/registry.json`
(`<framework>` is `/projects/framework` or `tools/framework`).

## Rules

- A new deploy, a move to another VPS or a domain change updates the registry in the same commit.
- Secrets stay out: Coolify tokens are in `~/.claude/mcp/coolify.json`, any read tokens in
  `~/.config/framework/deploy-secrets.json`.
- `registry verify` (a later step, `tools/scripts/deploy`) checks the registry against both Coolify: every uuid exists and no
  Coolify project is missing here.
- `deploy/observability/` (a later step) holds the logs/traces stack.
