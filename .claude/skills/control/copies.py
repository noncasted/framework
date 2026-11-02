# Copies of a project for agents that work in parallel: each has its own files and its own editor with its own MCP bridge.
# On btrfs a reflink copy, Library included, takes seconds and almost no disk. The copy holds .git and the uncommitted changes,
# so an agent commits there on its branch and the main tree takes the branch. What the copy has not delivered is kept in the
# project by saves.py.
import json
import os
import shutil
import subprocess
import time
from pathlib import Path

import editor
import saves
from common import ControlError
from saves import MARKER

DEFAULT_BASE = "/var/tmp/unity-copies"
SWEEP_MINUTES = 10
SKIP_TOP = {"builds", ".build"}
SKIP_CLIENT = {"Temp", "Logs", "Build", "BuildTelegram", "Builds", "MemoryCaptures", "ProfilerCaptures", "Recordings", "traces", "obj", "Library"}
SKIP_LIBRARY = {"BuildCache", "BuildHistory", "BuildPlayerData", "PlayerDataCache", "com.unity.web.stripping-tool", "ManagedCapture", "VP"}
CODE_FILES = ["*.cs", "*.cs.meta", "*.asmdef", "*.asmdef.meta", "*.asmref", "*.asmref.meta"]


def run_without_project(args):
    # The timer has no project: it walks all of them.
    if args.action == "sweep":
        return saves.sweep(default_base())
    return install()


def run(project, args):
    if args.action == "list":
        return list_copies(project, args.hung_after)
    if not args.name:
        raise ControlError(f"copy {args.action} needs a name")
    if args.action == "create":
        return create(project, args)
    if args.action == "drop":
        return drop(project, args.name)
    client = client_of(project, args.name)
    if args.action == "start":
        return editor.start(project, client, not args.window, args.force, args.hung_after, args.timeout)
    if args.action == "stop":
        return editor.stop(project, client, not args.no_save)
    if args.action == "sync":
        return sync(project, args.name, args.all)
    if args.action == "save":
        return save(project, args.name)
    return remove(project, args.name, not args.no_save, args.force)


def default_base():
    return os.environ.get("CONTROL_COPY_DIR") or DEFAULT_BASE


def copies_dir(project):
    base = os.environ.get("CONTROL_COPY_DIR") or project.config.get("editor", {}).get("copies", DEFAULT_BASE)
    return Path(base) / source_root(project).name


def source_root(project):
    # A command run from inside a copy still means the copies of the original project.
    marker = project.root / MARKER
    return Path(json.loads(marker.read_text())["source"]) if marker.exists() else project.root


def copy_root(project, name):
    if not name.replace("-", "").replace("_", "").isalnum():
        raise ControlError(f"bad name of a copy: {name}")
    return copies_dir(project) / name


def client_of(project, name):
    root = copy_root(project, name)
    if not (root / MARKER).exists():
        raise ControlError(f"no copy {name} at {root}: `control copy list`, `control copy create {name}`")
    return root / project.client.relative_to(project.root)


def create(project, args):
    root = copy_root(project, args.name)
    if root.exists():
        raise ControlError(f"{root} exists: `control copy start {args.name}` or `control copy remove {args.name}`")
    source = source_root(project)
    if args.name in saves.leftovers(source):
        raise ControlError(f"the project keeps saved work of a removed copy {args.name} and a new one would overwrite it: take the work, `control copy drop {args.name}`, or another name")
    client_name = str(project.client.relative_to(project.root))
    started = time.time()
    root.mkdir(parents=True)
    try:
        _copy_tree(source, root, client_name)
    except subprocess.CalledProcessError as error:
        shutil.rmtree(root)
        raise ControlError(f"a reflink copy failed (a copy without reflinks would be gigabytes): {error.stderr.strip()[:300]}") from error
    _clean(root, client_name)
    (root / MARKER).write_text(json.dumps({"source": str(source), "commit": _head(source), "created": time.strftime("%F %T")}, indent=2))
    saves.prepare(root, args.name, source)
    client = root / client_name
    result = {"copy": args.name, "root": str(root), "project": str(client), "seconds": round(time.time() - started)}
    if args.no_editor:
        return result
    timeout = 60 if args.no_wait else args.timeout
    started_editor = editor.start(project, client, not args.window, args.force, args.hung_after, timeout)
    return {**result, **started_editor}


def _copy_tree(source, root, client_name):
    for entry in source.iterdir():
        if entry.name in SKIP_TOP or entry.name == client_name.split("/")[0]:
            continue
        _reflink(entry, root / entry.name)
    client = source / client_name
    (root / client_name).mkdir(parents=True)
    for entry in client.iterdir():
        if entry.name not in SKIP_CLIENT:
            _reflink(entry, root / client_name / entry.name)
    if (client / "Library").exists():
        (root / client_name / "Library").mkdir()
        for entry in (client / "Library").iterdir():
            if entry.name not in SKIP_LIBRARY:
                _reflink(entry, root / client_name / "Library" / entry.name)


def _reflink(source, target):
    subprocess.run(["cp", "-a", "--reflink=always", str(source), str(target)], check=True, capture_output=True, text=True)


def _clean(root, client_name):
    # Locks of the processes that work in the source tree at the moment of the copy.
    for lock_file in (root / ".git").rglob("*.lock") if (root / ".git").is_dir() else []:
        lock_file.unlink()
    exclude = root / ".git" / "info" / "exclude"
    if exclude.parent.exists():
        with open(exclude, "a") as file:
            file.write(f"\n{MARKER}\n")


def _head(source):
    return subprocess.run(["git", "-C", str(source), "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()


def sync(project, name, everything):
    # Source -> copy, nothing is deleted, a file newer in the copy stays. The code by default: assets edited by the agent in the copy stay.
    client = client_of(project, name)
    source = source_root(project)
    root = copy_root(project, name)
    client_name = str(client.relative_to(root))
    filters = [] if everything else ["--include=*/"] + [f"--include={pattern}" for pattern in CODE_FILES] + ["--exclude=*"]
    pairs = [f"{client_name}/Assets", "shared", "tools/framework"]
    if everything:
        pairs.append(f"{client_name}/Packages")
    synced = []
    for relative in pairs:
        if (source / relative).exists():
            subprocess.run(["rsync", "-a", "--update", "--exclude=.git", "--exclude=Library", "--exclude=Temp", *filters, f"{source / relative}/", f"{root / relative}/"], check=True)
            synced.append(relative)
    return {"copy": name, "synced": synced, "mode": "all" if everything else "code", "note": "the editor of the copy picks it up on refresh_unity"}


def save(project, name):
    client_of(project, name)
    return {"copy": name, "pending": saves.save(copy_root(project, name), name, source_root(project))}


def remove(project, name, save_open_work, force):
    client = client_of(project, name)
    root = copy_root(project, name)
    source = source_root(project)
    _refuse_pending(saves.save(root, name, source), name, force)
    stopped = editor.stop(project, client, save_open_work)
    # The stop has just saved the open scenes.
    pending = saves.save(root, name, source)
    _refuse_pending(pending, name, force)
    file, digest = editor.status_file(client)
    file.unlink(missing_ok=True)
    (editor.STATUS_DIR / f"unity-mcp-port-{digest}.json").unlink(missing_ok=True)
    shutil.rmtree(root)
    return {"copy": name, "removed": str(root), "editor": stopped.get("was", "down"), "saved": pending if saves.is_pending(pending) else None}


def _refuse_pending(pending, name, force):
    if saves.is_pending(pending) and not force:
        raise ControlError(f"copy {name} holds work not delivered to the project ({len(pending['commits'])} commits, {pending['changed']} files): {pending['commits'][:5]} {pending['files'][:5]}. Take it into the main tree, or pass --force: the saved work stays in the project (copy/{name}/<branch>, refs/copies/{name}/wip)")


def drop(project, name):
    if (copy_root(project, name) / MARKER).exists():
        raise ControlError(f"copy {name} still exists: `control copy remove {name}` first")
    saves.drop(source_root(project), name)
    return {"copy": name, "dropped": True}


def install():
    # A systemd user timer of `copy sweep`. A later version of the script runs from the same path.
    script = Path(__file__).resolve().parent / "control"
    units = Path.home() / ".config" / "systemd" / "user"
    units.mkdir(parents=True, exist_ok=True)
    (units / "control-copy-sweep.service").write_text(f"[Unit]\nDescription=Save the work of the copies of the projects\n\n[Service]\nType=oneshot\nNice=15\nIOSchedulingClass=idle\nExecStart={script} copy sweep\n")
    (units / "control-copy-sweep.timer").write_text(f"[Unit]\nDescription=Save the work of the copies of the projects\n\n[Timer]\nOnStartupSec=1min\nOnUnitActiveSec={SWEEP_MINUTES}min\nPersistent=false\n\n[Install]\nWantedBy=timers.target\n")
    subprocess.run(["systemctl", "--user", "daemon-reload"], check=True)
    subprocess.run(["systemctl", "--user", "enable", "--now", "control-copy-sweep.timer"], check=True, capture_output=True)
    return {"timer": "control-copy-sweep.timer", "every_minutes": SWEEP_MINUTES, "script": str(script)}


def list_copies(project, hung_after):
    base = copies_dir(project)
    source = source_root(project)
    result = []
    for root in sorted(base.iterdir()) if base.exists() else []:
        if (root / MARKER).exists():
            client = root / project.client.relative_to(project.root)
            info = json.loads((root / MARKER).read_text())
            result.append({"copy": root.name, "root": str(root), "created": info["created"], "commit": info["commit"][:8], "pending": saves.save(root, root.name, source), **editor.status(client, hung_after)})
        elif root.is_dir():
            result.append({"copy": root.name, "root": str(root), "state": "no marker: being created, or made by hand and not saved"})
    known = {item["copy"] for item in result}
    for name in sorted(saves.leftovers(source) - known):
        result.append({"copy": name, "state": "removed", "saved": f"branches copy/{name}/*, snapshot refs/copies/{name}/wip; `control copy drop {name}` deletes them"})
    return result
