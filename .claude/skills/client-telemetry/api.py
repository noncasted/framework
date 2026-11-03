# GET over the reading API (backend/ClientTelemetry/Contract/CONTRACT.md). The token goes only in the Authorization header.
# Redirects are refused: urllib would carry the header to whatever host a redirect names.
import http.client
import json
import urllib.error
import urllib.parse
import urllib.request

from common import AUTH, NOT_FOUND, SERVICE, Failure
from config import TOKEN_NAME

_PREFIX = "/api/client-telemetry"
_TIMEOUT_SECONDS = 20
_PAGE = 1000


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *arguments, **named):
        return None


class Api:
    def __init__(self, url, token, env_file):
        self._url = url
        self._token = token
        self._env_file = env_file
        # Plain http is only local (config), the proxies of the environment must not see the header.
        proxies = {} if url.startswith("http://") else None
        self._opener = urllib.request.build_opener(urllib.request.ProxyHandler(proxies), _NoRedirect)

    def sessions(self, **filters):
        return self._get("/sessions", **filters)

    def all_sessions(self, **filters):
        items = []

        while True:
            page = self.sessions(**filters, offset=len(items), limit=_PAGE)
            items += page

            if len(page) < _PAGE:
                return items

    def session(self, session_id):
        return self._get(f"/sessions/{session_id}")

    def summary(self, session_id, first=None):
        return self._get(f"/sessions/{session_id}/summary", **{"from": first})

    def wait_samples(self, session_id, after, wait):
        return self._get(f"/sessions/{session_id}/samples", extra_timeout=wait, after=after, wait=wait, limit=_PAGE)

    def samples(self, session_id, after=0):
        return self._rows(f"/sessions/{session_id}/samples", after)

    def errors(self, session_id):
        return self._rows(f"/sessions/{session_id}/errors", 0)

    def marks(self, session_id):
        return self._rows(f"/sessions/{session_id}/marks", 0)

    def error_groups(self, limit):
        return self._get("/errors", limit=limit)

    def startups(self, limit):
        return self._get("/startups", limit=limit)

    def startup(self, startup_id):
        return self._get(f"/startups/{startup_id}")

    def _rows(self, path, after):
        rows = []

        while True:
            page = self._get(path, after=after, limit=_PAGE)

            if not page["items"]:
                return rows

            rows += page["items"]
            after = page["lastSeq"]

    def _get(self, path, extra_timeout=0, **query):
        given = {name: value for name, value in query.items() if value is not None}
        target = f"{self._url}{_PREFIX}{path}"
        full = f"{target}?{urllib.parse.urlencode(given, doseq=True)}" if given else target
        request = urllib.request.Request(full)
        request.add_unredirected_header("Authorization", f"Bearer {self._token}")

        try:
            with self._opener.open(request, timeout=_TIMEOUT_SECONDS + extra_timeout) as response:
                body = response.read()
        except urllib.error.HTTPError as error:
            raise self._status_failure(error, target) from None
        except (OSError, http.client.HTTPException) as error:
            raise Failure(SERVICE, f"cannot reach {target}: {getattr(error, 'reason', error)}") from None

        try:
            return json.loads(body)
        except ValueError:
            raise Failure(SERVICE, f"the answer of {target} is not JSON: wrong url, or a proxy in the way?") from None

    def _status_failure(self, error, target):
        status = error.code

        if status in (401, 403):
            return Failure(AUTH, f"{status} from {target}: the token was refused, check {TOKEN_NAME} in {self._env_file} (the value is not shown)")

        if status == 404:
            return Failure(NOT_FOUND, f"404 from {target}: no such session or startup (they are deleted after the retention), or the game does not serve the reading API")

        if 300 <= status < 400:
            return Failure(SERVICE, f"{status} redirect from {target}: not followed, the token must not travel to another address; set the final url")

        if status == 503:
            return Failure(SERVICE, f"503 from {target}: the backend cannot answer now (cluster not ready, a grain failed); a repeat may help")

        detail = error.read(300).decode("utf-8", "replace").strip()

        return Failure(SERVICE, f"{status} from {target}: {detail}")
