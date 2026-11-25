# The commands: each reads the registry, opens the stores of the server of the project (a tunnel unless --logs-url is
# given) for its own duration, runs its queries and prints. Every output line goes through print, the exit code is the result.
import json
import os
import time
from pathlib import Path

import api
import coolify
import queries
import registry
import registry_text
import text
import trace_text
import verify
from common import FINDINGS, NOT_FOUND, USAGE, Failure
from tunnel import Direct, Tunnel

_TRACE_ROWS = 2000
_FOLLOW_SECONDS = 2.0


def registry_show(args):
    data = registry.load(args.registry, args.refresh)
    name = args.name or registry.current(data, Path.cwd())

    if name:
        _print(registry_text.project(name, registry.find(data, name), data["servers"]))
    else:
        _print(registry_text.table(data))

    return 0


def registry_verify(args):
    data = registry.load(args.registry, args.refresh)
    panels = coolify.panels(args.coolify_config)
    answers = {}

    for key, server in data["servers"].items():
        if server["mcp"] not in panels:
            raise Failure(USAGE, f"no MCP server {server['mcp']} of {key} in {args.coolify_config}")

        panel = panels[server["mcp"]]
        answers[key] = (panel.origin, panel.lists())

    report = verify.compare(data, answers)
    _print(registry_text.verify(report))

    return FINDINGS if report.findings else 0


def errors(args):
    target, stores = _open(args)

    with stores as urls:
        found = api.logs(urls.logs, target.namespace, args.service).query(queries.errors(args.limit), args.since)

    _print([_where(target), *text.error_groups(found, args.since, target.namespace),
            *_cut(found, args.limit, f"(only the {args.limit} most frequent templates: raise --limit for the rest)")])

    return 0


def logs(args):
    target, stores = _open(args)

    with stores as urls:
        found = api.logs(urls.logs, target.namespace, args.service).query(args.query, args.since, args.limit)

    _print([_where(target), *text.rows(found), *_cut(found, args.limit)])

    return 0


def trace(args):
    target, stores = _open(args)

    with stores as urls:
        found = _spans(urls, target, args)

    _print([_where(target), *trace_text.tree(args.trace_id, found), *_cut(found, _TRACE_ROWS)])

    return 0


def around(args):
    target, stores = _open(args)

    with stores as urls:
        found_logs = api.logs(urls.logs, target.namespace).query(queries.trace_logs(args.trace_id), args.since, _TRACE_ROWS)
        found_spans = api.spans(urls.traces, target.namespace).query(queries.trace_spans(args.trace_id), args.since, _TRACE_ROWS)

    if not found_logs and not found_spans:
        raise Failure(NOT_FOUND, _absent(args.trace_id, target, args.since, "logs or spans"))

    _print([_where(target), *trace_text.timeline(args.trace_id, found_logs, found_spans),
            *_cut(found_logs, _TRACE_ROWS), *_cut(found_spans, _TRACE_ROWS)])

    return 0


def tail(args):
    target, stores = _open(args)

    with stores as urls:
        store = api.logs(urls.logs, target.namespace, args.service)
        found = store.query(queries.ALL, args.since, args.limit)
        _print([_where(target), *text.rows(found)])

        if args.follow:
            _follow(store, found)

    return 0


def _follow(store, seen_rows):
    # Polls from the time of the newest row; the rows of that very instant come again and are dropped by their content.
    last = max((row["_time"] for row in seen_rows), default=None)
    seen = {json.dumps(row, sort_keys=True) for row in seen_rows if row["_time"] == last}

    try:
        while True:
            time.sleep(_FOLLOW_SECONDS)
            found = store.query(queries.ALL, last or "1m")
            fresh = [row for row in found if json.dumps(row, sort_keys=True) not in seen]

            if fresh:
                _print(text.log_lines(fresh))
                last = max(row["_time"] for row in found)
                seen = {json.dumps(row, sort_keys=True) for row in found if row["_time"] == last}
    except KeyboardInterrupt:
        return


def _spans(urls, target, args):
    found = api.spans(urls.traces, target.namespace).query(queries.trace_spans(args.trace_id), args.since, _TRACE_ROWS)

    if not found:
        raise Failure(NOT_FOUND, _absent(args.trace_id, target, args.since, "spans"))

    return found


def _open(args):
    data = registry.load(args.registry, args.refresh)
    target = registry.target(data, args.project, Path.cwd(), args.namespace)

    if args.logs_url:
        return target, Direct(args.logs_url, args.traces_url)

    return target, Tunnel(target.server)


def _where(target):
    return f"[{target.name} on {target.server_key}, namespace {target.namespace}]"


def _absent(trace_id, target, since, what):
    return (f"no {what} of trace {trace_id} in namespace {target.namespace} on {target.server_key} over the last {since};"
            f" a span is searchable 20-30 s after the end of its trace, an older one needs --since (spans live 14d, logs 30d)")


def _cut(found, limit, note=None):
    note = note or f"(only the newest {limit} rows: narrow the query or the time)"

    return [note] if limit and len(found) >= limit else []


def _print(lines):
    print("\n".join(lines), flush=True)


def default_coolify_config():
    return os.path.expanduser(str(coolify.CONFIG))
