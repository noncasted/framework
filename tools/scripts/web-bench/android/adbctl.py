#!/usr/bin/env python3
"""Remote control of Chrome on an Android phone over USB: adb, the DevTools socket of Chrome, the Chrome DevTools Protocol.

Usage: adbctl.py [--device <name> | --serial <serial>] [--port <n>] <command> ...

  check               adb present, `adb devices -l`, the chosen device, Chrome installed, its DevTools socket open
  forward             adb forward tcp:<port> localabstract:chrome_devtools_remote (list and eval do it themselves)
  list                the pages of Chrome (localhost:<port>/json): index, id, url, title
  open <url>          opens the url in Chrome (am start -a android.intent.action.VIEW -d <url> <package>)
  eval '<js>' [--page <text>] [--timeout <s>]
                      Runtime.evaluate in a page over its websocket (stdlib only, no node or pip packages), the promise
                      awaited, the value printed as JSON; an exception of the page exits 1. --page: a part of the
                      url, title or id of the page (the first page by default); several matches are an error. No
                      answer in --timeout seconds (30) exits 1: a navigation started by the js itself (leave a page
                      with navigate or close), a promise that never settles, a page busy in a slow mode (?glcheck)
  navigate <url> [--page <text>] [--timeout <s>]
                      Page.navigate of that page to the url (about:blank stops a game without closing the tab)
  close [--page <text>]
                      closes the page (localhost:<port>/json/close/<id>): the game in it ends at once
  screenshot <out.png>
                      the screen of the phone (adb exec-out screencap -p), whatever app is on top

Options:
  --device  a name from ~/.config/device-lab/devices.json ({"devices": [{"name": ..., "kind": "android",
            "serial": null | "<adb serial>", "cdpPort": 9223}]}); serial null = the only connected device
  --serial  an adb serial directly (overrides the file)
  --port    the local port of the forward: --port, else cdpPort of the device, else 9222. The iOS proxy
            (ios_webkit_debug_proxy) takes 9222 too: with both phones give the Android one another port
Without --device / --serial: the only android device of devices.json, or (no file) the only connected device.
Environment: ADBCTL_PACKAGE - the Chrome package (com.android.chrome; com.chrome.beta, com.chrome.dev for others).

On the phone, once: Settings > About phone > tap "Build number" 7 times (developer options); Settings > System >
Developer options > "USB debugging" on; connect the USB cable, unlock the screen, accept "Allow USB debugging?" with
"Always allow from this computer". Chrome installed and started once (its DevTools socket exists only while Chrome
runs); the screen stays unlocked while measuring. On the machine: adb (Arch: `sudo pacman -S android-tools`, repo extra;
`android-udev` if the device shows as "no permissions").

The production meta refuses the Origin of a LAN build (403): a local build on the phone sends no client telemetry,
measure it with lan/remote-log.js or with eval here.
Example:
  adbctl.py check
  adbctl.py open "http://192.168.0.109:8770/?autorun=1"
  adbctl.py eval 'JSON.stringify({w: innerWidth, h: innerHeight, dpr: devicePixelRatio})' --page 8770
  adbctl.py screenshot /tmp/bench/android.png
"""
import argparse
import base64
import json
import os
import shlex
import shutil
import socket
import struct
import subprocess
import sys
import urllib.error
import urllib.request

DEVICES = os.path.expanduser('~/.config/device-lab/devices.json')
PACKAGE = os.environ.get('ADBCTL_PACKAGE', 'com.android.chrome')
SOCKET = 'chrome_devtools_remote'


def fail(message):
    print(f'adbctl: {message}', file=sys.stderr)
    sys.exit(1)


def require_adb():
    if not shutil.which('adb'):
        fail('adb is not installed. Install it: sudo pacman -S android-tools (or yay -S android-tools); '
             'add android-udev if the phone shows as "no permissions"')


def adb(serial, *args, binary=False, check=True):
    command = ['adb'] + (['-s', serial] if serial else []) + list(args)
    result = subprocess.run(command, capture_output=True, text=not binary)
    if check and result.returncode != 0:
        fail(f'{" ".join(command)} failed ({result.returncode}): {result.stderr.strip() if not binary else result.stderr}')
    return result.stdout


def connected():
    rows = []
    for line in adb(None, 'devices', '-l').splitlines()[1:]:
        if line.strip():
            parts = line.split()
            rows.append((parts[0], parts[1], ' '.join(parts[2:])))
    return rows


def device_entry(args):
    if not os.path.exists(DEVICES):
        if args.device:
            fail(f'--device {args.device}: {DEVICES} does not exist')
        return {}
    with open(DEVICES) as f:
        androids = [d for d in json.load(f)['devices'] if d['kind'] == 'android']
    if args.device:
        match = [d for d in androids if d['name'] == args.device]
        if not match:
            fail(f'no android device "{args.device}" in {DEVICES} (there: {[d["name"] for d in androids]})')
        return match[0]
    if len(androids) > 1:
        fail(f'several android devices in {DEVICES}, pick one with --device: {[d["name"] for d in androids]}')
    return androids[0] if androids else {}


def resolve(args):
    """The serial of the device to use (checked to be connected and authorized) and the forward port."""
    require_adb()
    entry = device_entry(args)
    serial = args.serial or entry.get('serial')
    port = args.port or entry.get('cdpPort') or 9222
    rows = connected()
    if serial is None:
        if len(rows) != 1:
            fail(f'{len(rows)} devices connected ({[r[0] for r in rows]}): connect one or pass --serial / --device')
        serial = rows[0][0]
    state = {r[0]: r[1] for r in rows}.get(serial)
    if state is None:
        fail(f'device {serial} is not connected (adb devices: {[r[0] for r in rows]})')
    if state != 'device':
        hint = {'unauthorized': 'unlock the phone and accept "Allow USB debugging?"',
                'no': 'no permissions: install android-udev, replug'}.get(state.split()[0], '')
        fail(f'device {serial} is "{state}" {hint}')
    return serial, port


def forward(serial, port):
    taken = subprocess.run(['ss', '-ltnpH', f'sport = :{port}'], capture_output=True, text=True).stdout.strip()
    ours = f'tcp:{port} localabstract:{SOCKET}' in adb(serial, 'forward', '--list')
    if taken and not ours:
        fail(f'local port {port} is taken by another process ({taken}); pass --port or cdpPort in devices.json')
    adb(serial, 'forward', f'tcp:{port}', f'localabstract:{SOCKET}')


def pages(port):
    try:
        with urllib.request.urlopen(f'http://localhost:{port}/json', timeout=5) as r:
            return [p for p in json.load(r) if p.get('type') == 'page']
    except (urllib.error.URLError, ConnectionError) as e:
        fail(f'no DevTools on localhost:{port} ({e}): is Chrome running on the phone? (adbctl.py check)')


class WebSocket:
    """The minimum of RFC 6455 a CDP client needs: one text message out, text messages in."""

    def __init__(self, url, timeout=30):
        rest = url.split('://', 1)[1]
        hostport, path = rest.split('/', 1)
        host, port = hostport.rsplit(':', 1)
        self.sock = socket.create_connection((host, int(port)), timeout=timeout)
        key = base64.b64encode(os.urandom(16)).decode()
        self.sock.sendall((f'GET /{path} HTTP/1.1\r\nHost: {hostport}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n'
                           f'Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n').encode())
        head = b''
        while b'\r\n\r\n' not in head:
            chunk = self.sock.recv(4096)
            if not chunk:
                fail(f'websocket {url} closed during the handshake')
            head += chunk
        if b' 101 ' not in head.split(b'\r\n', 1)[0]:
            fail(f'websocket {url} refused: {head.split(chr(13).encode(), 1)[0].decode()} (another DevTools client attached?)')
        self.buffer = head.split(b'\r\n\r\n', 1)[1]

    def _read(self, n):
        while len(self.buffer) < n:
            chunk = self.sock.recv(65536)
            if not chunk:
                fail('websocket closed by the phone')
            self.buffer += chunk
        data, self.buffer = self.buffer[:n], self.buffer[n:]
        return data

    def _send(self, opcode, payload):
        mask = os.urandom(4)
        n = len(payload)
        header = bytes([0x80 | opcode]) + (bytes([0x80 | n]) if n < 126 else
                                           bytes([0x80 | 126]) + struct.pack('>H', n) if n < 65536 else
                                           bytes([0x80 | 127]) + struct.pack('>Q', n))
        self.sock.sendall(header + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(payload)))

    def send(self, text):
        self._send(0x1, text.encode())

    def receive(self):
        message = b''
        while True:
            b0, b1 = self._read(2)
            n = b1 & 0x7F
            if n == 126:
                n = struct.unpack('>H', self._read(2))[0]
            elif n == 127:
                n = struct.unpack('>Q', self._read(8))[0]
            payload = self._read(n)
            opcode = b0 & 0x0F
            if opcode == 0x8:
                fail('websocket closed by the phone')
            if opcode == 0x9:
                self._send(0xA, payload)
                continue
            message += payload
            if b0 & 0x80:
                return message.decode('utf-8', 'replace')


def find_page(port, page_filter):
    found = pages(port)
    if page_filter:
        found = [p for p in found if page_filter in p.get('url', '') or page_filter in p.get('title', '')
                 or page_filter == p.get('id')]
    if not found:
        fail(f'no page{" matching " + repr(page_filter) if page_filter else ""} in Chrome (adbctl.py list)')
    if page_filter and len(found) > 1:
        fail(f'{len(found)} pages match {page_filter!r}: {[p["url"] for p in found]}')
    if 'webSocketDebuggerUrl' not in found[0]:
        fail(f'page {found[0]["url"]} has no webSocketDebuggerUrl: another DevTools client (chrome://inspect) holds it')
    return found[0]


def call(page, method, params, timeout=30):
    """One CDP command over the websocket of the page, its result."""
    ws = WebSocket(page['webSocketDebuggerUrl'].replace('localhost', '127.0.0.1'), timeout)
    ws.send(json.dumps({'id': 1, 'method': method, 'params': params}))
    try:
        while True:
            reply = json.loads(ws.receive())
            if reply.get('id') == 1:
                break
    except socket.timeout:
        fail(f'{method}: no answer of {page["url"]} in {timeout:.0f} s (the js navigated away itself, its promise never '
             f'settles or the page is busy - --timeout; leave a page with navigate or close)')
    if 'error' in reply:
        fail(f'CDP error: {reply["error"]}')
    return reply['result']


def evaluate(port, expression, page_filter, timeout):
    result = call(find_page(port, page_filter), 'Runtime.evaluate',
                  {'expression': expression, 'returnByValue': True, 'awaitPromise': True}, timeout)
    if 'exceptionDetails' in result:
        details = result['exceptionDetails']
        fail(f'exception in the page: {details.get("exception", {}).get("description") or details.get("text")}')
    value = result['result']
    print(json.dumps(value['value'], ensure_ascii=False) if 'value' in value else value.get('description', value['type']))


def check(args):
    require_adb()
    print(adb(None, 'version').splitlines()[0])
    print(adb(None, 'devices', '-l').rstrip())
    serial, port = resolve(args)
    model = adb(serial, 'shell', 'getprop', 'ro.product.model').strip()
    release = adb(serial, 'shell', 'getprop', 'ro.build.version.release').strip()
    print(f'device   {serial} ({model}, Android {release}), forward port {port}')
    if not adb(serial, 'shell', 'pm', 'path', PACKAGE, check=False).strip():
        fail(f'{PACKAGE} is not installed on {serial}')
    print(f'chrome   {PACKAGE} installed')
    if SOCKET not in adb(serial, 'shell', 'cat', '/proc/net/unix', check=False):
        fail(f'no DevTools socket @{SOCKET}: start Chrome on the phone (USB debugging must be on)')
    print(f'devtools @{SOCKET} open')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--device')
    parser.add_argument('--serial')
    parser.add_argument('--port', type=int)
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('check')
    sub.add_parser('forward')
    sub.add_parser('list')
    p = sub.add_parser('open')
    p.add_argument('url')
    p = sub.add_parser('eval')
    p.add_argument('js')
    p.add_argument('--page')
    p.add_argument('--timeout', type=float, default=30)
    p = sub.add_parser('navigate')
    p.add_argument('url')
    p.add_argument('--page')
    p.add_argument('--timeout', type=float, default=30)
    p = sub.add_parser('close')
    p.add_argument('--page')
    p = sub.add_parser('screenshot')
    p.add_argument('out')
    args = parser.parse_args()

    if args.command == 'check':
        check(args)
        return
    serial, port = resolve(args)
    if args.command == 'forward':
        forward(serial, port)
        print(f'localhost:{port} -> {serial} @{SOCKET}')
    elif args.command == 'list':
        forward(serial, port)
        for i, p in enumerate(pages(port)):
            print(f'{i}  {p["id"]}  {p["url"]}  {p.get("title", "")}')
    elif args.command == 'open':
        out = adb(serial, 'shell', 'am', 'start', '-a', 'android.intent.action.VIEW', '-d', shlex.quote(args.url), PACKAGE)
        if 'Error' in out:
            fail(out.strip())
        print(out.strip())
    elif args.command == 'eval':
        forward(serial, port)
        evaluate(port, args.js, args.page, args.timeout)
    elif args.command == 'navigate':
        forward(serial, port)
        page = find_page(port, args.page)
        result = call(page, 'Page.navigate', {'url': args.url}, args.timeout)
        if result.get('errorText'):
            fail(f'navigate {args.url}: {result["errorText"]}')
        print(f'{page["id"]}  {page["url"]} -> {args.url}')
    elif args.command == 'close':
        forward(serial, port)
        page = find_page(port, args.page)
        with urllib.request.urlopen(f'http://localhost:{port}/json/close/{page["id"]}', timeout=5) as r:
            r.read()
        print(f'closed {page["id"]}  {page["url"]}')
    elif args.command == 'screenshot':
        png = adb(serial, 'exec-out', 'screencap', '-p', binary=True)
        if not png.startswith(b'\x89PNG'):
            fail(f'screencap returned no PNG ({len(png)} bytes): {png[:200]!r}')
        with open(args.out, 'wb') as f:
            f.write(png)
        print(f'{args.out} ({len(png)} bytes)')


if __name__ == '__main__':
    main()
