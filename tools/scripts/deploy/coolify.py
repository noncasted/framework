# GET over the REST API of a Coolify panel (`<panel>/api/v1/...`) with the token of its MCP server in
# ~/.claude/mcp/coolify.json (mcpServers.<name>.headers.Authorization; the panel is the origin of its url).
# The token goes only in the Authorization header, never into an output; redirects are refused, so it reaches no other host.
import json
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

from common import AUTH, SERVICE, USAGE, Failure

CONFIG = Path("~/.claude/mcp/coolify.json")
LISTS = ("projects", "applications", "services", "databases")
_TIMEOUT_SECONDS = 30


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *arguments, **named):
        return None


class Panel:
    def __init__(self, mcp, url, authorization):
        parts = urllib.parse.urlsplit(url)
        self.mcp = mcp
        self.origin = f"{parts.scheme}://{parts.netloc}"
        self._authorization = authorization
        self._opener = urllib.request.build_opener(_NoRedirect)

    def lists(self):
        return {name: self._get(name) for name in LISTS}

    def _get(self, path):
        url = f"{self.origin}/api/v1/{path}"
        request = urllib.request.Request(url, headers={"Authorization": self._authorization, "Accept": "application/json"})

        try:
            with self._opener.open(request, timeout=_TIMEOUT_SECONDS) as response:
                text = response.read().decode()
        except urllib.error.HTTPError as error:
            code = AUTH if error.code in (401, 403) else SERVICE
            raise Failure(code, f"{self.mcp}: GET {url} -> {error.code} {error.reason}") from None
        except (urllib.error.URLError, OSError) as error:
            raise Failure(SERVICE, f"{self.mcp}: {url} unreachable: {getattr(error, 'reason', error)}") from None

        try:
            items = json.loads(text)
        except json.JSONDecodeError:
            raise Failure(SERVICE, f"{self.mcp}: GET {url} answered not JSON") from None

        if not isinstance(items, list):
            raise Failure(SERVICE, f"{self.mcp}: GET {url} answered {type(items).__name__}, not a list")

        return items


def panels(path):
    path = Path(path).expanduser()

    try:
        servers = json.loads(path.read_text())["mcpServers"]
    except OSError as error:
        raise Failure(USAGE, f"cannot read {path}: {error.strerror}") from None
    except (json.JSONDecodeError, KeyError):
        raise Failure(USAGE, f"{path} is not an MCP config with mcpServers") from None

    found = {}

    for name, server in servers.items():
        authorization = server.get("headers", {}).get("Authorization")

        if not authorization or "url" not in server:
            raise Failure(USAGE, f"{path}: mcpServers.{name} has no url or headers.Authorization")

        found[name] = Panel(name, server["url"], authorization)

    return found
