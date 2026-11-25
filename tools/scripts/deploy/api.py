# LogsQL over HTTP: `POST /select/logsql/query` of VictoriaLogs and of VictoriaTraces (its spans are LogsQL rows too).
# The project is never part of the query text: the store applies it as extra_stream_filters (logs) or extra_filters
# (spans), so no `or` of a query can reach another game of the host.
import json
import urllib.error
import urllib.parse
import urllib.request

from common import SERVICE, Failure

_TIMEOUT_SECONDS = 60


class Store:
    def __init__(self, name, url, scope):
        self._name = name
        self._url = f"{url}/select/logsql/query"
        self._scope = scope
        # The tunnel and a local stack are plain http on 127.0.0.1: the proxies of the environment are not for them.
        self._opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def query(self, logsql, since, limit=None):
        form = {"query": logsql, "start": since, **self._scope}

        if limit:
            form["limit"] = str(limit)

        body = urllib.parse.urlencode(form).encode()

        try:
            with self._opener.open(self._url, data=body, timeout=_TIMEOUT_SECONDS) as response:
                text = response.read().decode()
        except urllib.error.HTTPError as error:
            detail = error.read().decode(errors="replace").strip().splitlines()
            raise Failure(SERVICE, f"{self._name} {error.code}: {detail[0] if detail else error.reason}") from None
        except (urllib.error.URLError, OSError) as error:
            raise Failure(SERVICE, f"{self._name} at {self._url} unreachable: {getattr(error, 'reason', error)}") from None

        try:
            return [json.loads(line) for line in text.splitlines() if line.strip()]
        except json.JSONDecodeError:
            raise Failure(SERVICE, f"{self._name} answered not JSON lines: {text[:200]!r}") from None


def logs(url, namespace, service=None):
    stream = f'service.namespace="{namespace}"' + (f',service.name="{service}"' if service else "")

    return Store("VictoriaLogs", url, {"extra_stream_filters": "{" + stream + "}"})


def spans(url, namespace):
    return Store("VictoriaTraces", url, {"extra_filters": f'"resource_attr:service.namespace":="{namespace}"'})
