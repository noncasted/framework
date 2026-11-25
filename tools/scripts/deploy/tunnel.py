# The read ports of the stores of a server, bound to 127.0.0.1 of the host, forwarded by `ssh -N -L` on free local ports
# for the time of one command (deploy/observability/CLAUDE.md, "Reading: ssh tunnel"). No tokens: the ssh alias is the access.
import os
import socket
import subprocess
import time
import urllib.parse

from common import SERVICE, Failure

_READY_SECONDS = 20
_POLL_SECONDS = 0.1


class Stores:
    # Base URLs of VictoriaLogs and VictoriaTraces as this process reaches them.
    def __init__(self, logs, traces):
        self.logs = logs
        self.traces = traces


class Direct:
    # Stores reachable as they are (a local stack, the tests): nothing to open.
    def __init__(self, logs, traces):
        self._stores = Stores(logs, traces)

    def __enter__(self):
        return self._stores

    def __exit__(self, *exception):
        return False


class Tunnel:
    def __init__(self, server):
        self._alias = server["ssh"]
        self._remote = [_hostport(server["observability"][store]) for store in ("logs", "traces")]
        self._process = None

    def __enter__(self):
        ports = [_free_port() for _ in self._remote]
        forwards = []

        for port, (host, remote) in zip(ports, self._remote):
            forwards += ["-L", f"{port}:{host}:{remote}"]

        command = [os.environ.get("DEPLOY_SSH", "ssh"), "-N", "-o", "ExitOnForwardFailure=yes", "-o", "BatchMode=yes",
                   "-o", "ConnectTimeout=10", *forwards, self._alias]
        self._process = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE,
                                         text=True)

        try:
            self._wait(ports)
        except BaseException:
            # A failure or a signal before the `with` is entered: __exit__ will not run, the ssh is closed here.
            self._close()
            raise

        return Stores(*(f"http://127.0.0.1:{port}" for port in ports))

    def __exit__(self, *exception):
        self._close()

        return False

    def _wait(self, ports):
        deadline = time.monotonic() + _READY_SECONDS

        while time.monotonic() < deadline:
            if self._process.poll() is not None:
                error = self._process.stderr.read().strip()
                raise Failure(SERVICE, f"ssh {self._alias} failed (exit {self._process.returncode}): {error or 'no output'}")

            if all(_listening(port) for port in ports):
                return

            time.sleep(_POLL_SECONDS)

        raise Failure(SERVICE, f"ssh {self._alias}: the forwarded ports did not open in {_READY_SECONDS} s")

    def _close(self):
        if self._process.poll() is None:
            self._process.terminate()
            self._process.wait(timeout=10)

        self._process.stderr.close()


def _hostport(url):
    parts = urllib.parse.urlsplit(url)

    return parts.hostname, parts.port


def _free_port():
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))

        return probe.getsockname()[1]


def _listening(port):
    with socket.socket() as probe:
        probe.settimeout(0.5)

        return probe.connect_ex(("127.0.0.1", port)) == 0
