# Why an editor ended, and a watch that tells an agent about it. The cause comes from three places: the log of the editor (signal, native and
# managed frames), the core dump of the system (signal and the crashing thread, also when the log was cut) and the kernel log (out of memory).
import json
import re
import subprocess
import time

from common import STATE_DIR

FATAL = re.compile(r"Caught fatal signal|Fatal error|Unhandled exception|Assertion failed \(")
FRAME = re.compile(r"^#\d+\s")
MANAGED = re.compile(r"^\s+at .+|^Managed Stacktrace", re.M)
MAX_FRAMES = 30
SHUTDOWN_FRAMES = ("RuntimeCleanup", "PlayerCleanup", "CleanupEngine", "ExitEditor", "HandleProjectClose")
ACTIVE = ("ready", "busy", "starting")


def stopped_marker(pid):
    return STATE_DIR / f"stopped-by-control-{pid}"


def report(client, pid=None):
    log = _log_with_crash(client)
    result = {"project": str(client), "log": str(log) if log else None}
    if log:
        result.update(_read_log(log))
    core = _core_dump(client, pid)
    if core:
        result["core"] = core
    oom = _out_of_memory(pid or (core or {}).get("pid"))
    if oom:
        result["out_of_memory"] = oom
    result["cause"] = _summary(result)
    pid_of_core = (result.get("core") or {}).get("pid")
    if pid_of_core and stopped_marker(pid_of_core).exists():
        result["stopped_by_control"] = True
    return result


def _log_with_crash(client):
    logs = client / "Logs"
    if not logs.exists():
        return None
    candidates = [logs / "Editor.log", logs / "Editor-prev.log", *logs.glob("Editor-before-restart-*.log")]
    existing = sorted((file for file in candidates if file.exists()), key=lambda file: file.stat().st_mtime, reverse=True)
    for file in existing:
        if FATAL.search(_tail_text(file)):
            return file
    return existing[0] if existing else None


def _tail_text(file, size=2_000_000):
    with open(file, "rb") as stream:
        stream.seek(0, 2)
        stream.seek(max(0, stream.tell() - size))
        return stream.read().decode(errors="replace")


def _read_log(file):
    lines = _tail_text(file).splitlines()
    fatal = [index for index, line in enumerate(lines) if FATAL.search(line)]
    if not fatal:
        return {"fatal": None, "last_lines": lines[-40:]}
    # The first fatal line of the last run of them: a handler that crashes in turn repeats it with less to say.
    start = fatal[-1]
    while start - 1 in fatal or start - 2 in fatal:
        start -= 1
    after = lines[start:start + 400]
    frames = [line[:220] for line in after if FRAME.match(line)][:MAX_FRAMES]
    managed = [line[:220] for line in after if MANAGED.match(line)][:MAX_FRAMES]
    exceptions = [line[:300] for line in lines[max(0, start - 300):start] if "Exception" in line][-5:]
    return {
        "fatal": lines[start][:300],
        "before": [line[:220] for line in lines[max(0, start - 15):start]],
        "native_frames": frames,
        "managed_frames": managed,
        "exceptions_before": exceptions,
        "at_shutdown": any(name in frame for frame in frames for name in SHUTDOWN_FRAMES),
    }


def _core_dump(client, pid):
    output = subprocess.run(["coredumpctl", "list", "--json=short", "--no-pager"], capture_output=True, text=True).stdout
    if not output.strip():
        return None
    for entry in sorted(json.loads(output), key=lambda item: item["time"], reverse=True):
        if "Unity" not in entry.get("exe", "") or (pid and entry["pid"] != pid):
            continue
        info = subprocess.run(["coredumpctl", "info", "--no-pager", str(entry["pid"])], capture_output=True, text=True, errors="replace").stdout
        if f"-projectPath {client}" in info.replace("-projectpath", "-projectPath"):
            return {"pid": entry["pid"], "time": time.strftime("%F %T", time.localtime(entry["time"] / 1_000_000)), **_read_core(info)}
    return None


def _read_core(info):
    signal = re.search(r"Signal: (.+)", info)
    # The first stack trace is the thread that crashed; the frames above the handler of the signal say nothing about the cause.
    block = info.split("Stack trace of thread", 1)[1].split("\n\n", 1)[0] if "Stack trace of thread" in info else ""
    frames = [line.strip()[:160] for line in block.splitlines()[1:] if line.strip().startswith("#")]
    handlers = [index for index, frame in enumerate(frames) if "signal_handler" in frame or "HandleSignal" in frame]
    if handlers:
        frames = frames[handlers[-1] + 2:]    # also the frame of libc that entered the handler
    return {"signal": signal.group(1) if signal else None, "frames": frames[:MAX_FRAMES], "debug": "the skill diagnose-crash or `coredumpctl debug <pid>` for more"}


def _out_of_memory(pid):
    if not pid:
        return None
    output = subprocess.run(["journalctl", "-k", "-q", "--no-pager", "--since", "-1d", "--grep", f"Killed process {pid}"], capture_output=True, text=True).stdout
    return output.strip()[-300:] or None


def _summary(result):
    if result.get("out_of_memory"):
        return "the kernel killed it: out of memory"
    fatal = result.get("fatal")
    signal = (result.get("core") or {}).get("signal")
    if not fatal and not signal:
        return "no crash found in the log or the core dumps: killed from outside, or still running"
    if not fatal:
        return f"{signal}: the log has no trace of it, read the frames of the core dump"
    return f"{fatal}, " + ("at shutdown, after the work had ended" if result["at_shutdown"] else "during work")


def watch(client, status_of, hung_after, poll=5):
    # Blocks while the editor works and ends when it does not: run it with run_in_background and the agent is called on exit.
    first = status_of(client, hung_after)
    pid = first["pid"]
    if first["state"] in ACTIVE:
        while True:
            time.sleep(poll)
            current = status_of(client, hung_after)
            if current["state"] not in ACTIVE:
                break
            if current["pid"] != pid:
                return {**current, "event": "restarted", "previous_pid": pid, "note": "another process replaced the editor"}
    else:
        current = first
    marker = stopped_marker(pid) if pid else None
    if marker and marker.exists():
        marker.unlink()
        return {**current, "event": "stopped", "previous_pid": pid, "note": "stopped through control, not a failure"}
    event = "crashed" if current["state"] in ("down", "crashed") else current["state"]
    return {**current, "event": event, "previous_pid": pid, "report": report(client, pid) if event == "crashed" else None}

