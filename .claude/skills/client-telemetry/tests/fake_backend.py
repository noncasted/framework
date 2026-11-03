# The reading API of the contract (backend/ClientTelemetry/Contract/CONTRACT.md) on a local port, with the state a test sets.
import json
import threading
import time
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PREFIX = "/api/client-telemetry"


def session(session_id, status="live", platform="webgl", os_name="Android", started="2026-11-03T10:00:00Z", digest=None, tags=(), startup_id=None):
    return {
        "id": session_id,
        "status": status,
        "tags": list(tags),
        "device": {"platform": platform, "buildVersion": "1.0", "browser": "Chrome", "browserMajor": 120,
                   "os": os_name, "osMajor": 14, "gpu": "Adreno 640", "cores": 8, "memoryClass": 4,
                   "screenWidth": 1080, "screenHeight": 2400, "dpr": 2.75},
        "startedAt": started,
        "lastSampleAt": started,
        "ackSeq": 0,
        "names": [],
        "nameSlots": 0,
        "closedAt": None,
        "digest": digest,
        "startupId": startup_id,
    }


def startup_info(startup_id, final=True, name="Boot", duration=54337.0, frames=3033, span_count=3, depth=2, unfinished=0):
    return {
        "id": startup_id,
        "device": {"platform": "webgl", "buildVersion": "1.0", "browser": "Chrome", "browserMajor": 120,
                   "os": "Android", "osMajor": 14, "gpu": "Adreno 640", "cores": 8, "memoryClass": 4,
                   "screenWidth": 1080, "screenHeight": 2400, "dpr": 2.75},
        "name": name,
        "final": final,
        "receivedAt": "2026-11-03T10:00:00Z",
        "updatedAt": "2026-11-03T10:01:00Z",
        "durationMs": duration,
        "frames": frames,
        "spanCount": span_count,
        "depth": depth,
        "unfinishedSpans": unfinished,
        "droppedSpans": 2,
        "rejectedSpans": 1,
        "cutNames": 0,
    }


def startup_span(index, name, parent=-1, depth=0, duration=100.0, own=100.0, frames=1, unfinished=False):
    return {"index": index, "parentIndex": parent, "depth": depth, "name": name, "startMs": 0.0, "durationMs": duration,
            "ownMs": own, "frames": frames, "unfinished": unfinished, "allocBytes": 0}


def sample(seq, time_ms, values, hidden=False):
    return {"seq": seq, "time": time_ms, "hidden": hidden, "receivedAt": "2026-11-03T10:00:00Z", "values": values}


class FakeBackend:
    def __init__(self, token):
        self.token = token
        self.sessions = []
        self.samples = {}
        self.errors = {}
        self.marks = {}
        self.startups = []
        self.error_groups = {"totalGroups": 0, "countedSessions": 0, "sessionsWithoutDigest": 0, "omittedGroups": 0,
                             "omittedCount": 0, "droppedErrors": 0, "groups": []}
        self.requests = []
        self.authorizations = []
        self.forced = None
        self.list_calls = 0
        self.on_list = None
        self.on_samples = None
        self._server = ThreadingHTTPServer(("127.0.0.1", 0), self._handler())
        self.url = f"http://127.0.0.1:{self._server.server_port}"
        threading.Thread(target=self._server.serve_forever, daemon=True).start()

    def close(self):
        self._server.shutdown()
        self._server.server_close()

    def find(self, session_id):
        return next((item for item in self.sessions if item["id"] == session_id), None)

    def _answer(self, path, query):
        if self.forced:
            return self.forced

        if path == f"{PREFIX}/sessions":
            return self._list(query)

        if path == f"{PREFIX}/errors":
            return 200, self.error_groups, {}

        if path == f"{PREFIX}/startups":
            return 200, [page["startup"] for page in self.startups][:int(query.get("limit", "100"))], {}

        if path.startswith(f"{PREFIX}/startups/"):
            found = [page for page in self.startups if page["startup"]["id"] == path.rsplit("/", 1)[1]]

            return (200, found[0], {}) if found else (404, "", {})

        parts = path[len(PREFIX) + len("/sessions/"):].split("/")
        found = self.find(parts[0])

        if not found:
            return 404, "", {}

        if len(parts) == 1:
            return 200, found, {}

        if parts[1] == "summary":
            return 200, self._summary(found, query), {}

        if parts[1] == "samples":
            return self._samples(found, query)

        rows = (self.errors if parts[1] == "errors" else self.marks).get(found["id"], [])
        after = int(query.get("after", "0"))
        items = [row for row in rows if row["seq"] > after]

        return 200, {"status": found["status"], "lastSeq": items[-1]["seq"] if items else after, "items": items}, {}

    def _list(self, query):
        self.list_calls += 1

        if self.on_list:
            self.on_list(self.list_calls)

        items = [item for item in self.sessions
                 if query.get("status") in (None, item["status"])
                 and query.get("platform") in (None, item["device"]["platform"])
                 and all(tag in item["tags"] for tag in query["tag"])]
        offset = int(query.get("offset", "0"))

        return 200, items[offset:offset + int(query.get("limit", "100"))], {}

    def _summary(self, found, query):
        first = int(query.get("from", "0"))
        rows = [row for row in self.samples.get(found["id"], []) if row["time"] >= first]
        visible = [row for row in rows if not row["hidden"]]
        metrics = []

        for name in sorted({name for row in visible for name in row["values"]}):
            values = [row["values"][name] for row in visible if name in row["values"]]
            metrics.append({"name": name, "unit": "bytes" if name.startswith("memory.") else "ms", "count": len(values),
                            "average": sum(values) / len(values), "p50": sorted(values)[len(values) // 2],
                            "p95": max(values), "min": min(values), "max": max(values),
                            "growth": values[-1] - values[0] if name.startswith("memory.") else None})

        return {"samples": len(visible), "hiddenSamples": len(rows) - len(visible), "metrics": metrics}

    def _samples(self, found, query):
        if self.on_samples:
            self.on_samples(found)

        after = int(query.get("after", "0"))
        items = [row for row in self.samples.get(found["id"], []) if row["seq"] > after]

        # The wait of the real backend, short: the tests count seconds of the client.
        if not items and found["status"] == "live" and int(query.get("wait", "0")) > 0:
            time.sleep(0.05)

        return 200, {"status": found["status"], "lastSeq": items[-1]["seq"] if items else after, "items": items}, {}

    def _handler(self):
        backend = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                parsed = urllib.parse.urlparse(self.path)
                pairs = urllib.parse.parse_qsl(parsed.query)
                query = {**dict(pairs), "tag": [value for name, value in pairs if name == "tag"]}
                authorization = self.headers.get("Authorization")
                backend.requests.append(self.path)
                backend.authorizations.append(authorization)

                if authorization != f"Bearer {backend.token}":
                    self._send(401, "", {})
                else:
                    self._send(*backend._answer(parsed.path, query))

            def _send(self, status, body, headers):
                data = (body if isinstance(body, str) else json.dumps(body)).encode()
                self.send_response(status)

                for name, value in headers.items():
                    self.send_header(name, value)

                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)

            def log_message(self, *arguments):
                pass

        return Handler
