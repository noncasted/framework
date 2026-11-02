---
name: update-override-packages
description: Check the versions of the Unity packages we customize (embedded copies in client/Packages with local patches) against upstream and update them, re-applying our patches. Use when the user runs /update-override-packages, asks to update or check overridden / embedded / patched packages, after a Unity editor upgrade, or when a new package gets embedded and patched.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /update-override-packages, update override packages, обнови кастомные пакеты, проверь версии пакетов, обнови встроенные пакеты
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Update Override Packages Skill

Some packages live in `client/Packages/<name>/` as embedded copies with our own fixes. Unity never updates an
embedded package, so they go stale silently. This skill keeps them on the upstream version without losing the fixes.

The forks shared by all our games (addressables, render pipelines, ugui, web stripping tool, unity-mcp, MemoryPack,
UniTask) are not here: they live in `tools/framework/client/packages`, each with its `FORK.md` (update them there,
see `tools/framework/CLAUDE.md`). This skill covers the packages of the game only.

The script is in the skill, the data of the game in `.claude/override-packages/`:

| File | What |
|------|------|
| `packages.json` | The overridden packages: `source` (where the pristine package comes from), `base` (version or git commit the embedded copy is built on), `exclude` (entries not copied), `reason` (what our patch is for) |
| `patches/<name>.patch` | Our change on top of the pristine `base`, paths relative to the package root |
| `override_packages.py` | `status`, `diff`, `update` |

Sources: `builtin` - the package shipped with the project's editor (version from
`client/ProjectSettings/ProjectVersion.txt`, editors are searched in `$UNITY_EDITORS`, `/tools/unity`,
`~/Unity/Hub/Editor`), `base` is `<package version>@<editor version>` - editors ship different contents under the
same package version, so an editor upgrade is always an update; `registry` - `dist-tags.latest` of the registry; `git` - head of the branch `ref`, or the commit of a tag when `ref` is `refs/tags/<tag>`
(the `manifest.json` url stays pinned on `base` with `#<commit>`, `update` moves the pin); `repo` - no fetchable upstream
(Asset Store package such as A* Pathfinding Pro): the pristine copy is the package folder at the `base` commit of this
repository, `status` always says `up to date`. A new version of a `repo` package is imported by hand: commit the
pristine import, set `base` to that commit, re-apply the stored patch (`patch -p1`), run `diff`.

## Arguments

- no arguments - check everything, update what is behind
- package names - only these
- `check` / `status` - report only, change nothing

## Execution Steps

### Step 1 - Status

```bash
python3 .claude/skills/update-override-packages/override_packages.py status
```

Per package: source, base, `up to date` or `UPDATE -> <version>`, size of the patch. Notes to act on:

- `embedded copy differs from the stored patch` - someone edited the package since the last `diff`. Run
  `override_packages.py diff <name>`, read the new patch, make sure the change is intended, update `reason`.
- `base version is not installed` - a builtin package whose base editor is gone (editor upgraded): the stored patch
  cannot be verified, `update` applies it as is.

If the user asked only for a check - report the table and stop.

### Step 2 - Hand the editor over

An update rewrites package folders: the editor re-resolves, recompiles and reloads the domain. Run `ListAgents`; if
another session works in this repo, agree by `SendMessage` before going on. The editor must not be in Play mode.

### Step 3 - Update

```bash
python3 .claude/skills/update-override-packages/override_packages.py update <name>   # or --all
```

It refreshes the patch from the current embedded copy, replaces the folder with the pristine upstream version,
applies the patch, writes the new `base` to `packages.json` and bumps a plain version in `client/Packages/manifest.json`.

Exit code 2 - the patch did not apply cleanly. For every `*.rej` in the package: read the rejected hunk and the new
upstream code, make the same fix by hand (or drop it if upstream fixed the problem - then say so in the report),
delete the `.rej` / `.orig` files, then run `override_packages.py diff <name>` so the stored patch matches the new base.
Never leave a package half-patched.

### Step 4 - Verify in Unity

1. `manage_packages(action="resolve_packages")`, wait until `mcpforunity://editor/state` reports
   `data.advice.ready_for_tools`. When `com.coplaydev.unity-mcp` itself was updated the bridge reconnects - retry.
2. `read_console(types=["error"])` and `grep "error CS" client/Logs/Editor.log` over the new tail: no compile errors.
3. Check what the patch is for (`reason` in `packages.json`). For `com.coplaydev.unity-mcp`: call `execute_code`
   twice with different snippets - both must succeed with `"compiler":"roslyn"`.
4. `override_packages.py status` - everything `up to date`, no notes.

### Step 5 - Report

Table: package, old -> new version, patch applied cleanly / merged by hand / dropped. Do not commit unless asked.

## Adding a package to the overrides

1. Copy the pristine package to `client/Packages/<name>/` (from `client/Library/PackageCache/<name>@<hash>/`, or the
   editor's `BuiltInPackages/<name>`), leave `manifest.json` as it is - the embedded folder wins.
2. Add an entry to `packages.json`: `source`, `base` (the exact version; for builtin - `<version>@<editor>`; for git - the full commit from
   `packages-lock.json` `hash`, and pin the `manifest.json` url on it; for repo - the full commit of this repository with
   the pristine copy), `exclude`, `reason`.
3. Make the fix in the embedded copy, mark it in code with a `Local patch:` comment saying why.
4. `override_packages.py diff <name>` - the patch must contain only the fix. Unexpected hunks mean `base` or
   `exclude` is wrong.

Prefer fixing things on our side (our editor tools, our asset pipeline) over patching a package; embed only when the
package itself must behave differently.

## Removing an override

When upstream ships the fix: delete `client/Packages/<name>/` and its `.meta`, the entry in `packages.json` and the
patch; set the wanted version in `manifest.json`; resolve packages and check that `packages-lock.json` shows
`"source": "registry"` / `"git"` / `"builtin"` again.
