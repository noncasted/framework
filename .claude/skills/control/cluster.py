# The backend cluster of a project (Aspire). Values of the game: `cluster` of .claude/control.json.
import os
import subprocess
import time
import urllib.error
import urllib.request

from common import ControlError, Proc, alive, descendants, lock, port_owner, processes, spawn_detached, tail, terminate
from urllib.parse import urlparse

WAIT_TIMEOUT = 300
BUILD_TIMEOUT = 600
DEFAULT_PROJECT = "backend/Orchestration/Aspire/Aspire.csproj"
DEFAULT_PROCESSES = ["Aspire.AppHost", "Silo", "MetaGateway", "GameGateway", "ConsoleGateway", "Coordinator"]


def run(project, args):
    config = project.config.get("cluster")
    if not config or "health" not in config:
        raise ControlError(f"no cluster.health in {project.root}/.claude/control.json")
    cluster = Cluster(project, config)
    if args.action == "status":
        return cluster.status()
    if args.action == "logs":
        return {"log": str(cluster.log), "tail": tail(cluster.log, args.lines)}
    with lock(project, "cluster"):
        if args.action == "start":
            return cluster.start(args.timeout)
        if args.action == "stop":
            return cluster.stop()
        if args.action == "restart":
            stopped = cluster.stop()
            return {**cluster.start(args.timeout), "restarted": stopped["stopped"]}
        return cluster.rebuild(args.timeout)


class Cluster:
    def __init__(self, project, config):
        self.root = project.root
        self.csproj = config.get("project", DEFAULT_PROJECT)
        self.profile = config.get("profile", "http")
        self.health = config["health"]
        self.names = config.get("processes", DEFAULT_PROCESSES)
        self.log = project.state_file("cluster.log")
        self.pid_file = project.state_file("cluster.pid")

    def probe(self):
        try:
            with urllib.request.urlopen(self.health, timeout=5) as response:
                return response.status
        except urllib.error.HTTPError as error:
            return error.code
        except (urllib.error.URLError, OSError):
            return None

    def own_processes(self):
        # Only this tree: another project on this machine has processes with the same names.
        root = str(self.root)
        own = []
        for proc in processes():
            program = os.path.basename(proc.args[0])
            named = any(name in (program, os.path.basename(arg)) or f"{name}.dll" in arg for name in self.names for arg in proc.args[:3])
            if named and (proc.path_of("exe").startswith(root) or proc.path_of("cwd").startswith(root) or any(root in arg for arg in proc.args[:3])):
                own.append(proc)
        return own

    def launcher(self):
        if not self.pid_file.exists():
            return None
        pid = int(self.pid_file.read_text())
        return pid if alive(pid) else None

    def foreign_owner(self):
        # The port may be held by dcp, the proxy of Aspire. It is detached from the AppHost and names it in --monitor.
        pid = port_owner(urlparse(self.health).port)
        if pid is None:
            return None
        own = {proc.pid for proc in self.own_processes()}
        if self.launcher():
            own.add(self.launcher())
        current = pid
        while current > 1:
            proc = Proc(current)
            if current in own or proc.arg_after("--monitor") in {str(item) for item in own}:
                return None
            current = proc.ppid
        return pid

    def status(self):
        code = self.probe()
        own = self.own_processes()
        foreign = self.foreign_owner()
        state = "ready" if code == 200 and not foreign else "foreign" if foreign else "starting" if own or self.launcher() else "down"
        return {"state": state, "health": self.health, "code": code, "pids": [proc.pid for proc in own], "foreign_pid": foreign, "log": str(self.log)}

    def start(self, timeout, build=True):
        current = self.status()
        if current["state"] == "ready":
            return {**current, "started": False}
        if current["state"] == "foreign":
            cwd = os.readlink(f"/proc/{current['foreign_pid']}/cwd")
            raise ControlError(f"the port of {self.health} is held by pid {current['foreign_pid']} of another tree ({cwd}): the cluster of another project or copy")
        if current["state"] == "down":
            args = ["dotnet", "run", "--project", self.csproj, "--launch-profile", self.profile]
            if not build:
                args.append("--no-build")
            self.log.write_text("")
            self.pid_file.write_text(str(spawn_detached(args, self.log, self.root).pid))
        return self.wait(timeout)

    def wait(self, timeout):
        deadline = time.time() + timeout
        while time.time() < deadline:
            current = self.status()
            if current["state"] == "ready":
                return {**current, "started": True}
            if current["state"] == "down":
                return {**current, "error": "the cluster exited", "tail": tail(self.log, 40)}
            time.sleep(3)
        return {**current, "error": f"not ready in {timeout} s", "tail": tail(self.log, 40)}

    def stop(self):
        launcher = self.launcher()
        own = self.own_processes()
        if launcher is None and not own:
            return {"state": "down", "stopped": False}
        procs = processes()
        # The AppHost first: dcp stops the services with it. What is left after its grace goes by force.
        roots = [proc.pid for proc in own if "Aspire.AppHost" in " ".join(proc.args[:3])] + ([launcher] if launcher else [])
        family = {child.pid for pid in roots for child in descendants(pid, procs)}
        terminate(roots, 30)
        procs = processes()
        rest = [proc.pid for proc in procs if proc.pid in family] + [proc.pid for proc in self.own_processes()]
        killed = terminate(sorted(set(rest)), 10)
        self.pid_file.unlink(missing_ok=True)
        return {"state": "down", "stopped": True, "needed_sigkill": killed}

    def rebuild(self, timeout):
        stopped = self.stop()
        try:
            build = subprocess.run(["dotnet", "build", self.csproj, "-v", "q", "-nologo"], cwd=self.root, capture_output=True, text=True, timeout=BUILD_TIMEOUT)
        except subprocess.TimeoutExpired as error:
            raise ControlError(f"the build did not end in {BUILD_TIMEOUT} s") from error
        if build.returncode != 0:
            return {"state": "down", "error": "the build failed", "tail": build.stdout[-4000:]}
        return {**self.start(timeout, build=False), "rebuilt": True, "restarted": stopped["stopped"]}
