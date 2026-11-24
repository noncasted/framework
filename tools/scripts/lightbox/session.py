"""The window of the terminal that runs this Claude Code session: find the terminal, start its window, send a request."""
import fcntl
import json
import os
import shlex
import socket
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
RUNTIME = Path(os.environ.get("XDG_RUNTIME_DIR", "/tmp"))


def hyprctl_json(*args):
    return json.loads(subprocess.run(["hyprctl", *args, "-j"], check=True, capture_output=True, text=True).stdout)


def hypr_dispatch(lua):
    subprocess.run(["hyprctl", "dispatch", lua], check=True, capture_output=True)


def find_terminal():
    """The first ancestor that owns a window: (pid, workspace name, monitor id), or None for a detached session."""
    owners = {client["pid"]: client for client in hyprctl_json("clients")}
    pid = os.getpid()
    while pid > 1:
        if pid in owners:
            client = owners[pid]
            return pid, client["workspace"]["name"], client["monitor"]
        pid = int(Path(f"/proc/{pid}/stat").read_text().rsplit(")", 1)[1].split()[1])
    return None


class Session:
    def __init__(self):
        terminal = find_terminal()
        if terminal:
            self.terminal_pid, self.workspace, self.monitor = terminal
            key = str(self.terminal_pid)
        else:
            active = hyprctl_json("activeworkspace")
            self.terminal_pid, self.workspace, self.monitor = None, active["name"], active["monitorID"]
            key = "detached"
        self.app_id = f"lightbox.t{key}"
        self.socket = RUNTIME / f"lightbox-{key}.sock"
        self.cache = RUNTIME / f"lightbox-{key}"
        self.lock = RUNTIME / f"lightbox-{key}.lock"

    def connect(self):
        with open(self.lock, "w") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX)
            connection = self.try_connect()
            if connection:
                return connection
            self.start_app()
            for _ in range(200):
                connection = self.try_connect()
                if connection:
                    return connection
                time.sleep(0.05)
        raise SystemExit("lightbox: the window did not start")

    def try_connect(self):
        if not self.socket.exists():
            return None
        connection = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        try:
            connection.connect(str(self.socket))
            return connection
        except (ConnectionRefusedError, FileNotFoundError):
            connection.close()
            return None

    def start_app(self):
        self.socket.unlink(missing_ok=True)
        monitor = next(m for m in hyprctl_json("monitors") if m["id"] == self.monitor)
        width, height = int(monitor["width"] / monitor["scale"]), int(monitor["height"] / monitor["scale"])
        w, h = width * 45 // 100, height * 60 // 100
        # The move rule counts from the corner of the monitor of the window, so its offset is not added.
        x, y = width - w - 5, 31
        command = [sys.executable, str(HERE / "lightbox.py"), "app", "--app-id", self.app_id,
                   "--socket", str(self.socket), "--cache", str(self.cache),
                   "--monitor-size", f"{width}x{height}"]
        if self.terminal_pid:
            command += ["--terminal-pid", str(self.terminal_pid)]
        line = shlex.join(command)
        hypr_dispatch(f"hl.dsp.exec_cmd([==[{line}]==], {{ float = true, no_initial_focus = true, opacity = '1 1', "
                      f"workspace = '{self.workspace} silent', size = '{w} {h}', move = '{x} {y}' }})")


def request(message):
    connection = Session().connect()
    with connection:
        connection.sendall((json.dumps(message) + "\n").encode())
        reader = connection.makefile("r", encoding="utf-8")
        line = reader.readline()
    if not line:
        raise SystemExit("lightbox: the window closed without an answer")
    return json.loads(line)
