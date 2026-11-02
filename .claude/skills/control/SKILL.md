---
name: control
description: Start, stop, restart or rebuild the backend cluster and the Unity editor without asking the user, recover a hung or crashed editor, and give an agent its own copy of the project with a second editor and MCP for parallel testing. Use when a task needs the cluster or the editor, when MCP calls time out or find no editor, when the cluster must be rebuilt after a backend change, or when several agents test at the same time.
---

# Control

The editor and the cluster are tools of the agent. Start, restart and rebuild them yourself; do not ask the user and do not tell
them to do it. Run from the root of the project (or of a copy). Every command prints JSON; exit code 1 with `error` on a failure.

```
C=.claude/skills/control/control
```

## Cluster

```
$C cluster status | start | stop | restart | rebuild | logs [--lines N]
```

`start` returns when the health url answers 200; it does nothing when the cluster is up. `rebuild` stops, builds and starts: use it after a
backend change, a build failure comes back in `tail`. `foreign` means the port belongs to the cluster of another tree: the error names the
pid and the folder; that is not yours to stop, unless the task says so.

## Editor of the project

```
$C editor status | start | stop | restart | wait [--force] [--no-save] [--hung-after S] [--timeout S]
$C editor all
```

| State | Meaning | What to do |
|---|---|---|
| `ready` | the main thread runs, the bridge listens | work |
| `starting`, `busy` | no heartbeat, the logs still grow (import, compile, reload, tests) | `wait` |
| `hung` | no heartbeat and silent logs for `--hung-after` (300 s) | `restart` |
| `dialog` | silent for 60 s with a floating window of the editor, named in `dialogs` (Hyprland only); a long progress bar looks the same | `editor dialog`, see below |
| `crashed` | a window of `UnityBugReporter` for this project, the process may still hang on | `restart`: closes the window and starts anew |
| `stopped` | the process is frozen (`State: T`, a leftover debugger) | `restart` |
| `down` | no process | `start` |

- `restart` takes care of itself and is safe to call from several agents: they queue, the first restarts, the others find it ready. It does
  nothing to a `ready` editor and waits for a `busy` one, `--force` restarts anyway.
- The stop of a `ready` editor leaves Play, or saves scenes and assets when it is not in Play. `--no-save` skips that. A scene that was never saved stays
  unsaved and `saved` says so. A hung one cannot save: unsaved changes of its scenes are lost. The log goes to `Logs/Editor-before-restart-<time>.log` (`evidence`).
- Never call `EditorSceneManager.SaveOpenScenes()` or `SaveScene(scene)` without a path through `execute_code`: on a modified untitled scene (after `NewScene`) an editor
  with a window opens the modal `Save Scene` file dialog on the screen of the user. Skip scenes with an empty `path`, or save with an explicit one.
- A compile error at the start stops the editor on the dialog `Enter Safe Mode?`, with or without `-ignoreCompilerErrors`. `start`, `restart` and `wait` press `Ignore` there: the
  bridge comes up on the last good assemblies, `note` says so, the errors are read through MCP and fixed. An editor nobody waits for stays on the dialog: `$C editor wait`.

## Hidden windows

An editor with a window started here, the editor of the project included, has `CONTROL_HIDE_WINDOWS=1` in its environment. A `window.open` handler of the Hyprland
config of the user moves every window of such a process, dialogs included, to `special:agents` before it is drawn, and the focus stays where it was; `SUPER+ALT+A` shows that
workspace. Without the handler the windows appear on the current workspace. Measured there: MCP, Play at 60 fps, `CaptureScreenshot`, `editor dialog`, `editor answer`. An editor started before this variable keeps its window where it is until a restart.

## When the editor falls

`$C editor watch [--copy NAME]` with `run_in_background`, right after the editor is ready and for as long as the work needs it: it blocks while the
editor works and ends on a change, so the agent is called at that moment. One watch per editor per agent.

`event` of its result: `crashed` (with `report`), `hung`, `dialog`, `stopped`, `restarted` (the last two are the work of someone else, not a failure). Start a new watch after handling it.

`$C editor crash [--copy NAME]` gives the same `report` at any time, also after a restart. `cause` is the summary; `fatal`, `native_frames`, `managed_frames`, `exceptions_before`, `before` come from the log; `core` from the system, `out_of_memory` from the kernel.

- Find the code: `managed_frames` and `exceptions_before` first, then the game names in `native_frames`. Fix it, then `restart`. If the same operation crashed the editor before, do not repeat it in the same form; the table of `.claude/docs/UNITY_EDITOR_WORKFLOW.md` lists the known ones.
- `at_shutdown: true` is a crash after the work had ended, usually of Unity itself; it is not a cause to look for in the code of the task.
- Frames only in Unity or the runtime, with no managed code: the skill `diagnose-crash`, `coredumpctl debug <pid>`.

## Dialogs

A modal dialog of an editor with a window blocks its main thread: MCP calls time out and the state becomes `dialog`.

```
$C editor dialog [--copy NAME]          # floating windows of the editor: address, title, image
$C editor answer --button N [--address A] [--copy NAME]
```

1. Read each `image` with the Read tool: it shows the question and the buttons. A window such as `MCP Setup` floats without blocking anything; a progress bar passes by itself, so `wait`.
2. Choose the button that loses nothing: `Keep Changes` over `Discard Changes`, `Cancel` over a destructive action. When every button loses work of someone else, stop and report; use `restart` only for an editor of your own copy.
3. Buttons count by Tab from the one focused when the dialog opens: with two buttons left to right, with three (`DisplayDialogComplex`) ok, alt, cancel, which is not always the order on screen. 0 is Escape, it gives the cancel answer. `answered: false` means the window stayed: capture again.

To answer, the dialog takes the focus for about a second: `special:agents` opens over the monitor of the user and closes, the focus then returns. An editor without a window has no dialogs: `DisplayDialog` returns `false` at once, the second button. So in a copy without a window `Keep Changes` / `Discard Changes` means discard, and a dialog with a destructive second button does it silently.
- Before a third restart of the same failure in an hour, read the log: `.claude/docs/UNITY_EDITOR_WORKFLOW.md` of the game has the known hangs.

## Own copy for parallel work

The editor of the project is shared. An agent that changes scenes or assets, enters Play or compiles while others work takes a copy:

```
$C copy create <name>     # seconds on btrfs: files, .git and uncommitted changes, Library, then an editor without a window
$C copy list | start <name> | stop <name> | remove <name>
$C copy sync <name> [--all]         # project -> copy: the code, with --all also the assets; nothing is deleted, a file edited in the copy later than in the project stays
$C editor restart --copy <name>     # every editor command takes --copy
```

- `create` prints `root`, `project`, `port` and `unity_instance`. **Pass `unity_instance` in every MCP call** (`client@<hash>`, or the port).
  Never use `set_active_instance`: subagents of a session share one MCP server, and the choice of one would redirect the others.
- Edit the files under `root`, not the main tree. Inside the copy: `cd <root>`, `git switch -c <branch>`, commit. In the main tree: `git fetch <root> <branch>`.
- Check before a run: the result of `Application.dataPath` in `execute_code` must be inside `root`.
- Without a window Play works and input does not; screenshots need what the docs of the game say. `--window` starts with a window on the hidden workspace (see below).
- An editor needs about 6 GB free, `start` refuses below that (`--force` overrides). `remove` a copy when done: it stops the editor and deletes the folder with
  everything uncommitted in it.
- The cluster is not copied: all agents of the project share one.

## Values of the project

`.claude/control.json`: `cluster.health` (required for the cluster), `cluster.project`, `cluster.profile`, `cluster.processes`, `editor.project` (`client`),
`editor.install` (`/tools/unity/{version}/Editor/Unity` when not set), `editor.copies` (`/var/tmp/unity-copies`).
To run without prompts `.claude/settings.json` of the project has `"permissions": {"allow": ["Bash(.claude/skills/control/control *)"]}`.
