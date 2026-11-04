#!/usr/bin/env python3
"""Serves a Unity WebGL build over TLS on 127.0.0.1 the way the production nginx does, for the headless stand (run.mjs).

The stand maps the production host to this server (--host-resolver-rules=MAP <host>:443 127.0.0.1:<port>), so the page
runs with its production url and origin. Payloads *.br / *.gz go with Content-Encoding br / gzip and the type of the
file without the suffix; .wasm is application/wasm, .js application/javascript; Cache-Control: no-store.

The self-signed certificate of the host is made on the first start with openssl into
~/.local/share/device-lab/certs/<host>/{cert.pem,key.pem} ($DEVICE_LAB_HOME overrides ~/.local/share/device-lab);
run.mjs trusts exactly that key (--ignore-certificate-errors-spki-list).

Usage: serve_tls.py <build dir> [--port 8792] [--host <host>] [--project <game root>] [--config <device-lab.json>]
  --host     the production host of the game; default: "host" of <game root>/.claude/device-lab.json (game root = cwd)
  --port     default 8792; a second stand in parallel takes another port (and run.mjs --port the same)
Example: cd /projects/<game> && python3 tools/framework/tools/scripts/web-bench/stand/serve_tls.py builds/website --port 8792
Stop it by its pid (ss -ltnp 'sport = :8792'), not by pkill -f.
"""
import argparse
import functools
import http.server
import json
import os
import ssl
import subprocess
import sys


def cert_for(host):
    base = os.environ.get('DEVICE_LAB_HOME') or os.path.expanduser('~/.local/share/device-lab')
    folder = os.path.join(base, 'certs', host)
    cert, key = os.path.join(folder, 'cert.pem'), os.path.join(folder, 'key.pem')
    if not (os.path.exists(cert) and os.path.exists(key)):
        os.makedirs(folder, mode=0o700, exist_ok=True)
        subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '3650', '-subj', f'/CN={host}',
                        '-addext', f'subjectAltName=DNS:{host}', '-keyout', key, '-out', cert],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        os.chmod(key, 0o600)
        print(f'certificate of {host} made in {folder}', flush=True)
    return cert, key


class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map,
                      '.wasm': 'application/wasm', '.js': 'application/javascript', '.data': 'application/octet-stream'}
    encodings = {'.br': 'br', '.gz': 'gzip'}

    def guess_type(self, path):
        for suffix in self.encodings:
            if path.endswith(suffix):
                return super().guess_type(path[:-len(suffix)])
        return super().guess_type(path)

    def end_headers(self):
        path = self.path.split('?')[0]
        for suffix, encoding in self.encodings.items():
            if path.endswith(suffix):
                self.send_header('Content-Encoding', encoding)
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def log_message(self, *args):
        pass


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('dir')
    parser.add_argument('--port', type=int, default=8792)
    parser.add_argument('--host')
    parser.add_argument('--project', default='.')
    parser.add_argument('--config')
    a = parser.parse_args()
    if not os.path.isfile(os.path.join(a.dir, 'index.html')):
        sys.exit(f'serve_tls: no index.html in {a.dir}')
    host = a.host
    if not host:
        config = a.config or os.path.join(a.project, '.claude', 'device-lab.json')
        if not os.path.isfile(config):
            sys.exit(f'serve_tls: no --host and no game config {config}')
        host = json.load(open(config))['host']
    cert, key = cert_for(host)
    server = http.server.ThreadingHTTPServer(('127.0.0.1', a.port), functools.partial(Handler, directory=a.dir))
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.load_cert_chain(cert, key)
    server.socket = context.wrap_socket(server.socket, server_side=True)
    print(f'serving {os.path.abspath(a.dir)} as https://{host} on 127.0.0.1:{a.port} pid {os.getpid()}', flush=True)
    server.serve_forever()


if __name__ == '__main__':
    main()
