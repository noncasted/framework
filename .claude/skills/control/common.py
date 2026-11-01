# The project, its config, locks and the processes of this machine.
import fcntl
import hashlib
import json
import os
import signal
import subprocess
import time
from contextlib import contextmanager
from pathlib import Path

STATE_DIR = Path.home() / ".cache" / "claude-control"


class ControlError(Exception):
    pass


class Project:
    def __init__(self, root):
        self.root = Path(root)
        self.config = self._read_config()

    @staticmethod
    def detect():
        result = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True)
        if result.returncode != 0:
            raise ControlError("not inside a git repository: run from the root of the project")
        return Project(result.stdout.strip())

    @property
    def client(self):
        return self.root / self.config.get("editor", {}).get("project", "client")

    def state_file(self, kind):
        STATE_DIR.mkdir(parents=True, exist_ok=True)
        key = hashlib.sha1(str(self.root).encode()).hexdigest()[:10]
        return STATE_DIR / f"{self.root.name}-{key}.{kind}"

    def _read_config(self):
        path = self.root / ".claude" / "control.json"
        if not path.exists():
            return {}
        return json.loads(path.read_text(encoding="utf-8"))


@contextmanager
def lock(project, kind):
    # Two agents that find the same hung process queue here; the second one then finds it already restarted.
    with open(project.state_file(kind + ".lock"), "w") as file:
        fcntl.flock(file, fcntl.LOCK_EX)
        yield


class Proc:
    def __init__(self, pid):
        self.pid = pid
        self.args = (Path(f"/proc/{pid}/cmdline").read_bytes().decode(errors="replace")).split("\0")[:-1]
        stat = Path(f"/proc/{pid}/stat").read_text(errors="replace")
        fields = stat[stat.rindex(")") + 2:].split()
        self.state = fields[0]
        self.ppid = int(fields[1])
        self.started = os.stat(f"/proc/{pid}").st_mtime

    def path_of(self, link):
        try:
            return os.readlink(f"/proc/{self.pid}/{link}")
        except OSError:
            return ""

    def arg_after(self, name):
        lowered = [arg.lower() for arg in self.args]
        if name.lower() in lowered:
            index = lowered.index(name.lower()) + 1
            if index < len(self.args):
                return self.args[index]
        return None


def processes():
    result = []
    for entry in os.listdir("/proc"):
        if not entry.isdigit():
            continue
        try:
            proc = Proc(int(entry))
        except (OSError, ValueError):
            continue    # the process ended during the walk
        if proc.args:
            result.append(proc)
    return result


def descendants(pid, procs):
    found = []
    frontier = [pid]
    while frontier:
        parent = frontier.pop()
        for proc in procs:
            if proc.ppid == parent:
                found.append(proc)
                frontier.append(proc.pid)
    return found


def alive(pid):
    try:
        return Proc(pid).state != "Z"
    except (OSError, ValueError):
        return False


def send(pid, sig):
    try:
        os.kill(pid, sig)
    except ProcessLookupError:
        pass


def kill_now(pid):
    send(pid, signal.SIGKILL)
    deadline = time.time() + 10
    while time.time() < deadline and alive(pid):
        time.sleep(0.2)


def terminate(pids, grace):
    # TERM, wait, then KILL. A stopped process does not get TERM until it continues.
    for pid in pids:
        send(pid, signal.SIGCONT)
        send(pid, signal.SIGTERM)
    deadline = time.time() + grace
    while time.time() < deadline and any(alive(pid) for pid in pids):
        time.sleep(0.5)
    killed = [pid for pid in pids if alive(pid)]
    for pid in killed:
        send(pid, signal.SIGKILL)
    deadline = time.time() + 10
    while time.time() < deadline and any(alive(pid) for pid in killed):
        time.sleep(0.2)
    return killed


def spawn_detached(args, log_path, cwd, env=None):
    # A new session: the process outlives the shell of the agent, and the agent does not wait on its pipes.
    with open(log_path, "ab") as log:
        return subprocess.Popen(args, cwd=cwd, env=env, stdin=subprocess.DEVNULL, stdout=log, stderr=log, start_new_session=True)


def listening(pid):
    output = subprocess.run(["ss", "-tlnpH"], capture_output=True, text=True).stdout
    ports = []
    for line in output.splitlines():
        if f"pid={pid}," in line:
            ports.append(int(line.split()[3].rsplit(":", 1)[1]))
    return ports


def port_owner(port):
    output = subprocess.run(["ss", "-tlnpH", f"sport = :{port}"], capture_output=True, text=True).stdout
    for line in output.splitlines():
        if "pid=" in line:
            return int(line.split("pid=")[1].split(",")[0])
    return None


def tail(path, lines):
    if not Path(path).exists():
        return ""
    return subprocess.run(["tail", "-n", str(lines), str(path)], capture_output=True, text=True, errors="replace").stdout
