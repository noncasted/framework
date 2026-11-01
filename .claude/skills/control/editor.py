# The Unity editor of a project: state, start, stop, restart. The state comes from the heartbeat of the MCP bridge, which is
# written on the main thread twice a second: a ping of the bridge is answered off the main thread, so it passes on a hung editor.
import hashlib
import json
import os
import shutil
import subprocess
import time
from pathlib import Path

import crash
import dialogs
from bridge import bridge_command
from shutdown import bug_reporters, close_bug_reporters, exit_through_bridge, save_open_work
from common import ControlError, descendants, kill_now, listening, lock, processes, spawn_detached, terminate

HUNG_AFTER = 300
WAIT_TIMEOUT = 900
DIALOG_AFTER = 60
MIN_FREE_MEMORY_GB = 6
STATUS_DIR = Path(os.environ.get("UNITY_MCP_STATUS_DIR", Path.home() / ".unity-mcp"))


def run(project, args):
    if args.action == "all":
        return all_editors()
    target = target_of(project, args.copy)
    if args.action == "status":
        return status(target, args.hung_after)
    if args.action == "start":
        return start(project, target, args.headless, args.force, args.hung_after, args.timeout)
    if args.action == "stop":
        return stop(project, target, not args.no_save)
    if args.action == "restart":
        return restart(project, target, args.force, not args.no_save, args.headless, args.hung_after, args.timeout)
    if args.action == "crash":
        return crash.report(target)
    if args.action == "watch":
        return crash.watch(target, status, args.hung_after)
    if args.action in ("dialog", "answer"):
        proc = find_editor(target)
        if proc is None:
            raise ControlError(f"no editor of {target}")
        if args.action == "dialog":
            return dialogs.capture(proc.pid)
        if args.button is None:
            raise ControlError("answer needs --button")
        return dialogs.answer(proc.pid, args.button, args.address)
    return wait(target, args.hung_after, args.timeout)


def target_of(project, copy):
    if not copy:
        return project.client
    from copies import client_of
    return client_of(project, copy)


def find_editor(client):
    # The editor itself: neither its import workers nor the wrappers (`timeout`) that carry the same arguments.
    for proc in processes():
        if "unity" not in os.path.basename(proc.args[0]).lower():
            continue
        if proc.arg_after("-projectPath") == str(client) and proc.arg_after("-parentPid") is None and proc.state != "Z":
            return proc
    return None


def all_editors():
    result = []
    for proc in processes():
        path = proc.arg_after("-projectPath")
        if path and "unity" in os.path.basename(proc.args[0]).lower() and proc.arg_after("-parentPid") is None and proc.state != "Z":
            result.append(status(Path(path), HUNG_AFTER))
    return result


def status_file(client):
    digest = hashlib.sha1(f"{client}/Assets".encode()).hexdigest()[:8]
    return STATUS_DIR / f"unity-mcp-status-{digest}.json", digest


def seconds_since(path):
    try:
        return time.time() - path.stat().st_mtime
    except OSError:
        return None


def quiet_seconds_of_logs(client):
    logs = list((client / "Logs").glob("*.log")) if (client / "Logs").exists() else []
    ages = [age for age in (seconds_since(log) for log in logs) if age is not None]
    return min(ages) if ages else None


def status(client, hung_after):
    proc = find_editor(client)
    result = {"project": str(client), "state": "down", "pid": None, "port": None, "unity_instance": None, "log": str(client / "Logs" / "Editor.log")}
    if bug_reporters(client):
        return {**result, "state": "crashed", "pid": proc.pid if proc else None}
    if proc is None:
        return result
    file, digest = status_file(client)
    uptime = time.time() - proc.started
    heartbeat = seconds_since(file)
    if heartbeat is not None and heartbeat > uptime:
        heartbeat = None    # left by the previous run of the editor
    logs = quiet_seconds_of_logs(client)
    # The socket counts: the status file may name a port the bridge then failed to take.
    ports = [port for port in listening(proc.pid) if 6400 <= port < 6500]
    info = {}
    if heartbeat is not None:
        try:
            info = json.loads(file.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            pass    # read during a write of the editor; the next call reads it whole
    result.update(pid=proc.pid, uptime=round(uptime), heartbeat_age=_round(heartbeat), log_age=_round(logs), headless=any(arg.lower() == "-batchmode" for arg in proc.args))
    if ports:
        result["port"] = ports[0]
        result["unity_instance"] = f"{info.get('project_name', client.name)}@{digest}"
    result["state"] = _state(proc, uptime, heartbeat, logs, bool(ports), info.get("reloading", False), hung_after)
    if result["state"] in ("starting", "busy", "hung"):
        dialogs = _blocking_windows(proc.pid, uptime, heartbeat, logs)
        if dialogs:
            result.update(state="dialog", dialogs=dialogs)
    return result


def _blocking_windows(pid, uptime, heartbeat, logs):
    # A floating window of the editor while its main thread and logs are silent. A long progress bar looks the same, hence the wait.
    quiet = min(value for value in (uptime, heartbeat, logs) if value is not None)
    if quiet < DIALOG_AFTER:
        return []
    return [client["title"] for client in dialogs.floating_windows(pid)]


def _state(proc, uptime, heartbeat, logs, has_port, reloading, hung_after):
    if proc.state == "T":
        return "stopped"
    if heartbeat is not None and heartbeat < 15 and has_port and not reloading:
        return "ready"
    quiet = min(value for value in (uptime, heartbeat, logs) if value is not None)
    if quiet >= hung_after:
        return "hung"
    return "starting" if heartbeat is None else "busy"


def _round(value):
    return None if value is None else round(value)


def wait(client, hung_after, timeout):
    deadline = time.time() + timeout
    while True:
        result = status(client, hung_after)
        if result["state"] == "ready":
            return result
        if result["state"] in ("down", "hung", "stopped", "dialog", "crashed"):
            return {**result, "error": f"the editor is {result['state']}; log: {result['log']}"}
        if time.time() >= deadline:
            return {**result, "error": f"not ready in {timeout} s"}
        time.sleep(3)


def unity_binary(project, client):
    version_file = client / "ProjectSettings" / "ProjectVersion.txt"
    version = next(line.split(":", 1)[1].strip() for line in version_file.read_text().splitlines() if line.startswith("m_EditorVersion:"))
    template = project.config.get("editor", {}).get("install")
    candidates = [template] if template else ["/tools/unity/{version}/Editor/Unity", str(Path.home() / "Unity/Hub/Editor/{version}/Editor/Unity")]
    for candidate in candidates:
        path = candidate.format(version=version)
        if os.access(path, os.X_OK):
            return path
    raise ControlError(f"no Unity {version}: set editor.install in .claude/control.json")


def free_memory_gb():
    for line in Path("/proc/meminfo").read_text().splitlines():
        if line.startswith("MemAvailable:"):
            return int(line.split()[1]) / 1024 / 1024
    raise ControlError("no MemAvailable in /proc/meminfo")


def launch(project, client, headless):
    binary = unity_binary(project, client)
    (client / "Temp" / "UnityLockfile").unlink(missing_ok=True)
    (client / "Logs").mkdir(exist_ok=True)
    # Without it a compile error stops the start: the Safe Mode dialog in a window, an exit in batch mode.
    args = [binary, "-projectPath", str(client), "-ignoreCompilerErrors"]
    log = client / "Logs" / "control-launch.log"
    # A crash ends the process: no window of the bug reporter for nobody to close.
    env = {**os.environ, "UNITY_DONOTSTARTBUGREPORTER": "1"}
    if headless:
        # Without -nographics: the Tilemap renderer crashes with it. The bridge refuses a batch editor unless it is allowed.
        spawn_detached(args + ["-batchmode", "-logFile", str(client / "Logs" / "Editor.log")], log, client, {**env, "UNITY_MCP_ALLOW_BATCH": "1"})
        return
    spawn_detached(args, log, client, env)


def start(project, client, headless, force, hung_after, timeout):
    with lock(project, f"editor-{hash_of(client)}"):
        return _start(project, client, headless, force, hung_after, timeout)


def _start(project, client, headless, force, hung_after, timeout):
    if find_editor(client) is not None:
        return wait(client, hung_after, timeout)
    free = free_memory_gb()
    if free < MIN_FREE_MEMORY_GB and not force:
        raise ControlError(f"{free:.1f} GB of memory is free, an editor needs about {MIN_FREE_MEMORY_GB}: stop a copy or pass --force")
    launch(project, client, headless)
    deadline = time.time() + 30
    while time.time() < deadline and find_editor(client) is None:
        time.sleep(1)
    if find_editor(client) is None:
        return {"project": str(client), "state": "down", "error": "the editor did not start in 30 s", "log": str(client / "Logs" / "control-launch.log")}
    return wait(client, hung_after, timeout)


def hash_of(client):
    return hashlib.sha1(str(client).encode()).hexdigest()[:10]


def stop(project, client, save):
    with lock(project, f"editor-{hash_of(client)}"):
        return _stop(client, save)


def _stop(client, save):
    proc = find_editor(client)
    if proc is None:
        return {"project": str(client), "state": "down", "stopped": False, "closed_bug_reporters": close_bug_reporters(client)}
    current = status(client, HUNG_AFTER)
    saved = None
    if save and current["state"] == "ready" and current["port"]:
        saved = save_open_work(current["port"])
    procs = processes()
    workers = [other.pid for other in procs if other.arg_after("-parentPid") == str(proc.pid)]
    family = [child.pid for child in descendants(proc.pid, procs)]
    # SIGTERM crashes this editor at shutdown: a bug reporter window and no exit for over a minute. So Exit when it answers, SIGKILL when not.
    crash.stopped_marker(proc.pid).touch()
    exited = current["state"] == "ready" and current["port"] and exit_through_bridge(current["port"], proc.pid)
    if not exited:
        kill_now(proc.pid)
    terminate(sorted(set(workers + family) - {proc.pid}), 5)
    closed = close_bug_reporters(client)
    (client / "Temp" / "UnityLockfile").unlink(missing_ok=True)
    return {"project": str(client), "state": "down", "stopped": True, "pid": proc.pid, "was": current["state"], "saved": saved, "needed_sigkill": not exited, "closed_bug_reporters": closed}


def restart(project, client, force, save, headless, hung_after, timeout):
    with lock(project, f"editor-{hash_of(client)}"):
        current = status(client, hung_after)
        if not force:
            if current["state"] == "ready":
                return {**current, "restarted": False, "note": "the editor is ready: nothing to restart, --force restarts it anyway"}
            if current["state"] in ("starting", "busy"):
                current = wait(client, hung_after, timeout)
                if current["state"] == "ready":
                    return {**current, "restarted": False, "note": "the editor was busy and is ready now"}
        evidence = _keep_evidence(client)
        stopped = _stop(client, save)
        result = _start(project, client, headless, True, hung_after, timeout)
        return {**result, "restarted": True, "was": stopped.get("was", "down"), "saved": stopped.get("saved"), "evidence": evidence}


def _keep_evidence(client):
    log = client / "Logs" / "Editor.log"
    if not log.exists():
        return None
    target = client / "Logs" / f"Editor-before-restart-{time.strftime('%Y%m%d-%H%M%S')}.log"
    shutil.copyfile(log, target)
    return str(target)
