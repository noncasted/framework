# A trace for an agent: the span tree by parent_span_id, and the logs and spans of one trace on one time line.
# Span rows are the LogsQL rows of VictoriaTraces: `_time` is the start, `duration` in ns, `status_code` 2 is an error,
# span attributes `span_attr:<name>`, events `event:event_name:<i>` / `event:event_attr:<name>:<i>`.
import re

import text

_ERROR = "2"
_EVENT_ATTRIBUTE = re.compile(r"^event:event_attr:(.+):(\d+)$")


def service(span):
    return span.get("resource_attr:service.name", "-")


def status(span):
    if span.get("status_code") != _ERROR:
        return ""

    return "  ERROR" + (f" {span['status_message']}" if span.get("status_message") else "")


def tree(trace_id, spans):
    by_id = {span["span_id"]: span for span in spans}
    children = {}

    for span in spans:
        parent = span.get("parent_span_id")
        children.setdefault(parent if parent in by_id else None, []).append(span)

    first = min(spans, key=lambda span: span["_time"])
    start = int(first["start_time_unix_nano"])
    end = max(int(span["end_time_unix_nano"]) for span in spans)
    errors = sum(span.get("status_code") == _ERROR for span in spans)
    services = sorted({service(span) for span in spans})
    lines = [f"trace {trace_id}: {len(spans)} spans, {text.duration(end - start)}, {errors} errors, services {', '.join(services)},"
             f" started {text.time(first['_time'])} UTC (offset, duration, service, span)"]

    def walk(span, depth):
        offset = text.duration(int(span["start_time_unix_nano"]) - start)
        orphan = f"  (parent {span['parent_span_id']} not found)" if depth == 0 and span.get("parent_span_id") else ""
        lines.append(f"+{offset:>9} {text.duration(span['duration']):>9}  {service(span)}  {'  ' * depth}{span['name']}{status(span)}{orphan}")
        lines.extend(f"{' ' * 22}{'  ' * depth}{line}" for line in details(span))

        for child in sorted(children.get(span["span_id"], []), key=lambda item: item["_time"]):
            walk(child, depth + 1)

    for root in sorted(children.get(None, []), key=lambda item: item["_time"]):
        walk(root, 0)

    return lines


def details(span):
    # The attributes and the events of a failed span; a span that did not fail shows none.
    if span.get("status_code") != _ERROR:
        return []

    attributes = [f"{key[len('span_attr:'):]}={value}" for key, value in sorted(span.items()) if key.startswith("span_attr:")]
    lines = ["  ".join(attributes)] if attributes else []

    for _, event in sorted(events(span).items(), key=lambda item: int(item[0])):
        name = event.pop("name", "event")

        if "exception.type" in event or "exception.message" in event:
            lines.append(f"! {event.pop('exception.type', '-')}: {event.pop('exception.message', '')}")
            event.pop("exception.stacktrace", None)
        else:
            lines.append(f"{name} " + "  ".join(f"{key}={value}" for key, value in sorted(event.items())))

    return lines


def events(span):
    found = {}

    for key, value in span.items():
        if key.startswith("event:event_name:"):
            found.setdefault(key.rsplit(":", 1)[1], {})["name"] = value
            continue

        match = _EVENT_ATTRIBUTE.match(key)

        if match:
            found.setdefault(match.group(2), {})[match.group(1)] = value

    return found


def timeline(trace_id, logs, spans):
    # Every record with its details right under it: the exception of a log, the attributes and events of a failed span.
    entries = [(row["_time"], [_log(row), *text.exception(row)]) for row in logs]
    entries += [(span["_time"], [_span(span), *(f"    {line}" for line in details(span))]) for span in spans]
    lines = [f"trace {trace_id}: {len(spans)} spans, {len(logs)} logs (UTC)"]

    for _, block in sorted(entries, key=lambda entry: entry[0]):
        lines += block

    return lines


def _log(row):
    return f"{text.time(row['_time'])} {row.get('service.name', '-')} {text.severity(row)} {row['_msg']}"


def _span(span):
    return f"{text.time(span['_time'])} {service(span)} SPAN {span['name']} {text.duration(span['duration'])}{status(span)}"
