# deploy

`registry.json` is the map of every deployment of the games: which VPS hosts which project, the Coolify uuids, the public
domains, the deploy branch. Sessions and scripts (logs, deploy, `claude-coolify`) read it to know where to go. No secrets.
`tools/scripts/deploy/prod registry show [<project>]` prints one project with its server (skill `backend-logs`).

## Schema

- `servers.<key>`: `ovh` (Poland) and `firstvds` (Kazakhstan); the same key names the MCP server (`coolify-<key>`), the ssh
  alias and `backend/measurements`. Fields: country, city, provider, ip, mcp, ssh, ssh_user, docker (the command: ovh needs
  `sudo -n docker`), panel (Coolify URL), `observability` (`logs` / `traces` read URLs on 127.0.0.1 of the host, `access:
  ssh-tunnel`: read through `ssh -L`, no tokens; `coolify`: project uuid, name, environment, `application` uuid of the stack,
  branch, GitHub App).
- `projects.<repository name>`: `framework` (false for post-radio, storytale, nikita-botik), `server`, `mode` (`services` |
  `monolith`), `namespace` (`service.namespace` of logs, = the key), `coolify` (project uuid, name, environment),
  `repository`, `branch`, optional `compose` (the compose file of each layout, `services` / `monolith`, in the deploy
  branch: `backend/measurements/tools/local.sh` runs them), `resources` (uuid, type application / service / database, role), `domains` by role, optional `note`.
- `neon-dodge` has no deploy: it is listed with `"deployed": false` and no server.
- `storytale` is v1: never redeploy or touch it.

## Reading

A game submodule lags behind main, so scripts read the latest main, not the working tree:
`git -C <framework> fetch -q origin main && git show origin/main:deploy/registry.json`
(`<framework>` is `/projects/framework` or `tools/framework`).

## Rules

- A new deploy, a move to another VPS or a domain change updates the registry in the same commit.
- Secrets stay out: Coolify tokens are in `~/.claude/mcp/coolify.json`; the logs and traces are read over ssh, with no token.
- `tools/scripts/deploy/prod registry verify` (skill `backend-logs`) checks the registry against both Coolify: every uuid
  exists with its type and project name, and no Coolify project or resource is missing here. Run it after a change of the
  registry or of a Coolify project; it reads origin/main, so a change not pushed yet is checked with
  `--registry deploy/registry.json`.
- `deploy/observability/` holds the logs/traces stack, installed on both servers (its README.md).
- `backend/measurements/` measures the memory of the deployed backends (a read-only snapshot over the ssh aliases of
  `servers`) and of a local Release run of the deploy branch of a project (`tools/local.sh`); how to repeat each in its
  `README.md`, the numbers in `RESULTS.md`.
