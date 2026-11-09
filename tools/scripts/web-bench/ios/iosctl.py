#!/usr/bin/env python3
"""Drives a Safari tab of an iPhone / iPad over USB through ios_webkit_debug_proxy (WebKit inspector protocol).

Usage: iosctl.py --device <name> [--page N] <command> ...
  list                       pages of Safari: index, title, url
  eval '<js>'                evaluates an expression in the page; prints the value (a string as is, else JSON);
                             a thrown exception is printed to stderr with exit code 1
  open <url>                 navigates the page (location.href=...; Page.navigate does nothing on iOS 16)
  reload                     reloads the page
  console <seconds>          prints the console messages of the page for that long
  wait '<js>' [--timeout S]  waits until the expression is truthy (default 120 s), exit 1 on timeout
  wait-page [--timeout S]    waits until the proxy lists a page (after a restart of Safari; default 20 s)
  proxy start|stop|status    ios_webkit_debug_proxy -c <udid>:<proxyPort> -F in the background, pid file and log in
                             ~/.local/share/device-lab/run/<device>-iwdp.{pid,log}; status: exit 0 running, 3 stopped

The device (udid, proxyPort) comes from ~/.config/device-lab/devices.json (see devicelab.py). Needs the screen unlocked,
Settings > Safari > Advanced > Web Inspector on, and websocket-client (installed into the venv by setup.sh; run this with
~/.local/share/device-lab/pmd/bin/python or any python that has it).

iOS 16+ multiplexes the page behind Target.*: the first Target.targetCreated gives the page id, every command goes
through Target.sendMessageToTarget and the answers come in Target.dispatchMessageFromTarget. Older iOS answer directly.

Example:
  iosctl.py --device ipad7 proxy start
  iosctl.py --device ipad7 open 'https://example.com/?autorun=1'
  iosctl.py --device ipad7 eval 'JSON.stringify({w: document.querySelector("canvas").width, dpr: devicePixelRatio})'
"""
import argparse
import json
import os
import signal
import subprocess
import sys
import time
import urllib.error
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import devicelab  # noqa: E402


class CommandError(Exception):
    pass


def import_websocket():
    try:
        import websocket
    except ImportError:
        raise devicelab.ConfigError(
            f"websocket-client is missing in {sys.executable}: run it with {devicelab.pmd_python()} (setup.sh)")
    return websocket


def proxy_url(device):
    return f"http://localhost:{device['proxyPort']}"


def pages(device):
    try:
        with urllib.request.urlopen(proxy_url(device) + "/json", timeout=5) as r:
            return json.load(r)
    except (urllib.error.URLError, ConnectionError) as e:
        raise CommandError(f"no ios_webkit_debug_proxy on {proxy_url(device)} ({e}): "
                           f"iosctl.py --device {device['name']} proxy start")


def page(device, index):
    listed = pages(device)
    if not listed:
        raise CommandError("the proxy lists no Safari page: device connected and unlocked, Web Inspector on, a tab open?")
    if index >= len(listed):
        raise CommandError(f"page {index} does not exist, {len(listed)} listed")
    return listed[index]


class Tab:
    def __init__(self, ws_url):
        websocket = import_websocket()
        self.timeout_error = websocket.WebSocketTimeoutException
        self.ws = websocket.create_connection(ws_url, timeout=10)
        self.next_id = 1
        self.target = None
        self.events = []
        # iOS 16+ announces the page target first; without it within 3 s the tab answers directly.
        deadline = time.time() + 3
        self.ws.settimeout(0.5)
        while self.target is None and time.time() < deadline:
            try:
                self._handle(json.loads(self.ws.recv()))
            except self.timeout_error:
                pass
        self.ws.settimeout(10)

    def _handle(self, msg):
        if msg.get("method") == "Target.targetCreated":
            info = msg["params"]["targetInfo"]
            if info.get("type") == "page":
                self.target = info["targetId"]
            return None
        if msg.get("method") == "Target.dispatchMessageFromTarget":
            return self._handle(json.loads(msg["params"]["message"]))
        if "id" in msg:
            return msg
        self.events.append(msg)
        return None

    def call(self, method, params=None, timeout=15):
        cid = self.next_id
        self.next_id += 1
        inner = {"id": cid, "method": method, "params": params or {}}
        if self.target:
            outer = {"id": 100000 + cid, "method": "Target.sendMessageToTarget",
                     "params": {"targetId": self.target, "message": json.dumps(inner)}}
            self.ws.send(json.dumps(outer))
        else:
            self.ws.send(json.dumps(inner))
        deadline = time.time() + timeout
        while time.time() < deadline:
            msg = self._handle(json.loads(self.ws.recv()))
            if msg and msg.get("id") == cid:
                if "error" in msg:
                    raise CommandError(f"{method}: {msg['error']}")
                return msg.get("result")
        raise CommandError(f"{method}: no answer in {timeout} s")

    def evaluate(self, expression):
        result = self.call("Runtime.evaluate", {"expression": expression, "returnByValue": True})
        if result.get("wasThrown") or result.get("exceptionDetails"):
            value = result.get("result", {})
            raise CommandError("the page threw: " + str(value.get("description") or value.get("value") or result))
        return result.get("result", {}).get("value")

    def pump(self, seconds):
        end = time.time() + seconds
        self.ws.settimeout(0.5)
        while time.time() < end:
            try:
                self._handle(json.loads(self.ws.recv()))
            except self.timeout_error:
                pass
            for e in self.events:
                if e.get("method") == "Console.messageAdded":
                    m = e["params"]["message"]
                    print(f"[{m.get('level')}] {m.get('text')}", flush=True)
            self.events.clear()


def print_value(value):
    print(value if isinstance(value, str) else json.dumps(value, ensure_ascii=False))


def connected_udids():
    result = subprocess.run(["idevice_id", "-l"], capture_output=True, text=True)
    if result.returncode != 0:
        return []
    return result.stdout.split()


def proxy_files(device):
    run = devicelab.run_dir()
    return run / f"{device['name']}-iwdp.pid", run / f"{device['name']}-iwdp.log"


def proxy_pid(device):
    pid_file, _ = proxy_files(device)
    if not pid_file.exists():
        return None
    pid = int(pid_file.read_text().strip())
    try:
        with open(f"/proc/{pid}/cmdline", "rb") as f:
            cmdline = f.read()
    except FileNotFoundError:
        pid_file.unlink()
        return None
    if b"ios_webkit_debug_proxy" not in cmdline:
        pid_file.unlink()
        return None
    return pid


def proxy(device, action):
    pid_file, log_file = proxy_files(device)
    pid = proxy_pid(device)
    if action == "status":
        if pid:
            print(f"running pid={pid} port={device['proxyPort']} log={log_file}")
            return 0
        print("stopped")
        return 3
    if action == "stop":
        if not pid:
            print("stopped")
            return 0
        os.kill(pid, signal.SIGTERM)
        for _ in range(50):
            if not os.path.exists(f"/proc/{pid}"):
                break
            time.sleep(0.1)
        else:
            raise CommandError(f"ios_webkit_debug_proxy pid {pid} did not stop on SIGTERM")
        pid_file.unlink()
        print(f"stopped pid={pid}")
        return 0
    if pid:
        print(f"already running pid={pid} port={device['proxyPort']}")
        return 0
    if device["udid"] not in connected_udids():
        raise CommandError(f"device {device['name']} ({device['udid']}) is not connected over USB (idevice_id -l)")
    with open(log_file, "ab") as log:
        process = subprocess.Popen(
            ["ios_webkit_debug_proxy", "-c", f"{device['udid']}:{device['proxyPort']}", "-F"],
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, start_new_session=True)
    pid_file.write_text(str(process.pid))
    for _ in range(50):
        if process.poll() is not None:
            pid_file.unlink()
            raise CommandError(f"ios_webkit_debug_proxy exited with {process.returncode}, see {log_file}")
        try:
            urllib.request.urlopen(proxy_url(device) + "/json", timeout=1).close()
            print(f"started pid={process.pid} port={device['proxyPort']} log={log_file}")
            return 0
        except (urllib.error.URLError, ConnectionError):
            time.sleep(0.1)
    raise CommandError(f"ios_webkit_debug_proxy pid {process.pid} does not answer on {proxy_url(device)}, see {log_file}")


def wait_page(device, timeout):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            if pages(device):
                return 0
        except CommandError:
            pass
        time.sleep(1)
    raise CommandError(f"no Safari page on {proxy_url(device)} in {timeout} s")


def wait_truthy(device, index, expression, timeout):
    deadline = time.time() + timeout
    while time.time() < deadline:
        if Tab(page(device, index)["webSocketDebuggerUrl"]).evaluate(f"!!({expression})"):
            return 0
        time.sleep(1)
    raise CommandError(f"'{expression}' is not truthy after {timeout} s")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--device", required=True, help="device name in devices.json")
    parser.add_argument("--page", type=int, default=0, help="index of the page in `list` (default 0)")
    parser.add_argument("--timeout", type=float, help="timeout of wait / wait-page, seconds")
    parser.add_argument("command", choices=["list", "eval", "open", "reload", "console", "wait", "wait-page", "proxy"])
    parser.add_argument("argument", nargs="?")
    args = parser.parse_args()
    device = devicelab.ios_device(args.device)

    def argument(name):
        if args.argument is None:
            raise CommandError(f"{args.command} needs <{name}>")
        return args.argument

    if args.command == "proxy":
        action = argument("start|stop|status")
        if action not in ("start", "stop", "status"):
            raise CommandError("proxy start|stop|status")
        return proxy(device, action)
    if args.command == "list":
        for i, p in enumerate(pages(device)):
            print(i, p.get("title"), p.get("url"))
        return 0
    if args.command == "wait-page":
        return wait_page(device, args.timeout or 20)
    if args.command == "wait":
        return wait_truthy(device, args.page, argument("js"), args.timeout or 120)
    tab = Tab(page(device, args.page)["webSocketDebuggerUrl"])
    if args.command == "eval":
        print_value(tab.evaluate(argument("js")))
    elif args.command == "open":
        print_value(tab.evaluate(f"location.href = {json.dumps(argument('url'))}; 'ok'"))
    elif args.command == "reload":
        print_value(tab.evaluate("location.reload(); 'ok'"))
    elif args.command == "console":
        tab.call("Console.enable")
        tab.pump(float(argument("seconds")))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (devicelab.ConfigError, CommandError) as e:
        print(f"error: {e}", file=sys.stderr)
        sys.exit(1 if isinstance(e, CommandError) else 2)
