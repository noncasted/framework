#!/usr/bin/env python3
"""Serves a local Unity WebGL build to the phones of the LAN like the production nginx and takes the remote log of the page.

Usage: serve.py <build-dir> [--port 8770] [--bind 0.0.0.0] [--log <file>]

  <build-dir>   the folder with index.html (a copy of a build, see inject.sh: the copy gets remote-log.js)
  --port        8770 by default. The firewall (ufw) of the machine lets the LAN reach only tcp 8770-8771
                (plus 34997-34999, 54997-55511): on another port the phone silently fails to open the page,
                so any other port prints a loud warning
  --bind        address to listen on, 0.0.0.0 by default
  --log         the file of POST /log, <build-dir>/../log.txt by default; appended, one line per entry:
                "HH:MM:SS <client ip> <body>" (the time of the arrival on this machine)

Serving like nginx: *.br gets Content-Encoding: br and *.gz Content-Encoding: gzip, with the type of the file under the
compression (application/wasm for .wasm, application/javascript for .js, application/octet-stream for .data), so the
compressed build of Unity loads without its decompression fallback; every answer is Cache-Control: no-cache (index.html
must be fresh after a rebuild, and the non-hashed files of StreamingAssets too).

Telemetry: the production meta answers 403 to the Origin http://<lan ip>:<port>, so a local build sends no client
telemetry; a measurement of a local build reads the remote log only (remote-log.js, then windows.py).

Prints the LAN url (the IPv4 address of every physical interface: no VPN, docker or bridge) and the pid; stop it with kill <pid>.
Example:
  cp -r <game>/builds/website /tmp/bench/site && tools/scripts/web-bench/lan/inject.sh /tmp/bench/site
  tools/scripts/web-bench/lan/serve.py /tmp/bench/site --log /tmp/bench/log.txt &
"""
import argparse
import functools
import http.server
import os
import subprocess
import sys
import threading
import time

OPEN_PORTS = [(8770, 8771), (34997, 34999), (54997, 55511)]
TYPES = {'.wasm': 'application/wasm', '.js': 'application/javascript', '.data': 'application/octet-stream',
         '.json': 'application/json', '.mjs': 'application/javascript'}
ENCODINGS = {'.br': 'br', '.gz': 'gzip'}


def lan_addresses():
    # The IPv4 addresses of the physical interfaces (the ones with a device in sysfs): no VPN, docker or bridge.
    result = []
    for name in sorted(os.listdir('/sys/class/net')):
        if not os.path.exists(f'/sys/class/net/{name}/device'):
            continue
        out = subprocess.run(['ip', '-4', '-o', 'addr', 'show', 'dev', name], capture_output=True, text=True, check=True).stdout
        result += [(name, line.split()[3].split('/')[0]) for line in out.splitlines()]
    return result


class Handler(http.server.SimpleHTTPRequestHandler):
    log_file = None
    log_lock = threading.Lock()
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map, **TYPES}

    def _encoding(self):
        return ENCODINGS.get(os.path.splitext(self.path.split('?')[0])[1])

    def guess_type(self, path):
        stem, ext = os.path.splitext(path)
        return super().guess_type(stem if ext in ENCODINGS else path)

    def end_headers(self):
        encoding = self._encoding()
        if encoding:
            self.send_header('Content-Encoding', encoding)
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()

    def do_POST(self):
        if self.path.split('?')[0] != '/log':
            self.send_error(404)
            return
        body = self.rfile.read(int(self.headers.get('Content-Length', 0))).decode('utf-8', 'replace')
        line = f"{time.strftime('%H:%M:%S')} {self.client_address[0]} {body.replace(chr(10), ' | ')}\n"
        with self.log_lock, open(self.log_file, 'a') as f:
            f.write(line)
        self.send_response(204)
        self.end_headers()

    def log_message(self, format, *args):
        if self.command != 'POST':
            super().log_message(format, *args)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('build_dir')
    parser.add_argument('--port', type=int, default=8770)
    parser.add_argument('--bind', default='0.0.0.0')
    parser.add_argument('--log')
    args = parser.parse_args()

    build_dir = os.path.abspath(args.build_dir)
    if not os.path.isfile(os.path.join(build_dir, 'index.html')):
        sys.exit(f'serve.py: no index.html in {build_dir}')
    log_file = os.path.abspath(args.log or os.path.join(build_dir, '..', 'log.txt'))
    if not os.path.isdir(os.path.dirname(log_file)):
        sys.exit(f'serve.py: the folder of the log {os.path.dirname(log_file)} does not exist')
    if not any(low <= args.port <= high for low, high in OPEN_PORTS):
        bar = '!' * 100
        print(f'{bar}\nWARNING: port {args.port} is closed to the LAN by ufw (open: 8770-8771, 34997-34999, 54997-55511).\n'
              f'A phone will silently fail to open the page; only this machine can reach it.\n{bar}', file=sys.stderr)

    Handler.log_file = log_file
    try:
        server = http.server.ThreadingHTTPServer((args.bind, args.port), functools.partial(Handler, directory=build_dir))
    except OSError as e:
        sys.exit(f'serve.py: cannot listen on {args.bind}:{args.port}: {e} (taken? see: ss -ltnp | grep :{args.port})')
    print(f'serving {build_dir}', flush=True)
    if args.bind != '0.0.0.0':
        print(f'  url  http://{args.bind}:{args.port}/')
    else:
        addresses = lan_addresses()
        if not addresses:
            print('WARNING: no IPv4 address on a physical interface, the phones cannot reach this machine', file=sys.stderr)
        for name, address in addresses:
            print(f'  url  http://{address}:{args.port}/  ({name})')
    print(f'  log  {log_file}\n  pid  {os.getpid()}', flush=True)
    server.serve_forever()


if __name__ == '__main__':
    main()
