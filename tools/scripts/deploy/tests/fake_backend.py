# Local fakes of what the script talks to: the LogsQL endpoint of a store (VictoriaLogs or VictoriaTraces) and the REST
# lists of a Coolify panel. They answer what a test sets, mostly the answers recorded from the real services (fixtures/),
# and keep every request for the asserts.
import json
import threading
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

FIXTURES = Path(__file__).resolve().parent / "fixtures"


def rows(name):
    return [json.loads(line) for line in (FIXTURES / name).read_text().splitlines() if line.strip()]


class _Server:
    def __init__(self, handler):
        self._server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
        self._server.fake = self
        self.url = f"http://127.0.0.1:{self._server.server_address[1]}"
        self.port = self._server.server_address[1]
        self.requests = []
        threading.Thread(target=self._server.serve_forever, kwargs={"poll_interval": 0.05}, daemon=True).start()

    def close(self):
        self._server.shutdown()
        self._server.server_close()


class FakeStore(_Server):
    # answer(form) -> a list of rows, or (status, text) for a refusal; the default answers nothing.
    def __init__(self):
        super().__init__(_StoreHandler)
        self.answer = lambda form: []


class FakePanel(_Server):
    def __init__(self, token, lists):
        super().__init__(_PanelHandler)
        self.token = token
        self.lists = lists
        self.status = {}  # path -> status to answer instead of the list
        self.redirect = None  # a URL every request is sent to with 302


class _Quiet(BaseHTTPRequestHandler):
    def log_message(self, *arguments):
        pass

    def _send(self, status, body, content_type="application/json"):
        data = body.encode()
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


class _StoreHandler(_Quiet):
    def do_POST(self):
        fake = self.server.fake

        if self.path != "/select/logsql/query":
            return self._send(404, "not found", "text/plain")

        length = int(self.headers.get("Content-Length", 0))
        form = {key: values[0] for key, values in urllib.parse.parse_qs(self.rfile.read(length).decode()).items()}
        fake.requests.append(form)
        answer = fake.answer(form)

        if isinstance(answer, tuple):
            return self._send(answer[0], answer[1], "text/plain")

        self._send(200, "".join(json.dumps(row) + "\n" for row in answer), "application/stream+json")


class _PanelHandler(_Quiet):
    def do_GET(self):
        fake = self.server.fake
        fake.requests.append({"path": self.path, "authorization": self.headers.get("Authorization")})

        if fake.redirect:
            self.send_response(302)
            self.send_header("Location", fake.redirect)
            self.end_headers()
            return

        if self.headers.get("Authorization") != f"Bearer {fake.token}":
            return self._send(401, json.dumps({"message": "Unauthenticated."}))

        name = self.path.removeprefix("/api/v1/")

        if name in fake.status:
            return self._send(fake.status[name], json.dumps({"message": "fake refusal"}))

        if name not in fake.lists:
            return self._send(404, json.dumps({"message": "Not found."}))

        self._send(200, json.dumps(fake.lists[name]))
