# The forms of log rows for an agent: one line per record, the exception under it, groups of errors, other rows as key=value.
import json

_SEVERITIES = ((21, "FATAL"), (17, "ERROR"), (13, "WARN"), (9, "INFO"), (5, "DEBUG"), (1, "TRACE"))
_STACK_LINES = 6
_SKIPPED = ("_stream", "_stream_id")


def time(value):
    # 2026-11-25T05:25:17.534845658Z -> 11-25 05:25:17.534 (UTC, as stored).
    return f"{value[5:10]} {value[11:23]}" if len(value) >= 23 else value


def severity(row):
    number = int(row.get("severity_number") or 0)

    return next((name for low, name in _SEVERITIES if number >= low), row.get("severity_text") or "-")


def duration(nanoseconds):
    milliseconds = int(nanoseconds) / 1e6

    return f"{milliseconds / 1000:.2f}s" if milliseconds >= 1000 else f"{milliseconds:.1f}ms"


def is_log(row):
    return "_time" in row and "_msg" in row


def log_lines(rows, stack=True):
    lines = []

    for row in sorted(rows, key=lambda item: item["_time"]):
        trace = f"  trace={row['trace_id']}" if row.get("trace_id") else ""
        lines.append(f"{time(row['_time'])} {row.get('service.name', '-')} {severity(row)} {row['_msg']}{trace}")
        lines += exception(row, stack)

    return lines


def exception(row, stack=True):
    if not row.get("exception.type") and not row.get("exception.message"):
        return []

    lines = [f"    ! {row.get('exception.type', '-')}: {row.get('exception.message', '')}"]

    if stack and row.get("exception.stacktrace"):
        frames = row["exception.stacktrace"].splitlines()
        lines += [f"      {frame.strip()}" for frame in frames[:_STACK_LINES]]

        if len(frames) > _STACK_LINES:
            lines.append(f"      ... {len(frames) - _STACK_LINES} more")

    return lines


def plain_rows(rows):
    # Rows of a query with pipes (stats, fields): every field but the internal stream ones.
    return ["  ".join(f"{key}={value}" for key, value in row.items() if key not in _SKIPPED) for row in rows]


def rows(rows, stack=True):
    if not rows:
        return ["no rows"]

    return log_lines(rows, stack) if all(is_log(row) for row in rows) else plain_rows(rows)


def error_groups(rows, since, namespace):
    if not rows:
        return [f"no warnings or errors of {namespace} in the last {since}"]

    total = sum(int(row["hits"]) for row in rows)
    lines = [f"{total} warnings and errors of {namespace} in the last {since}, {len(rows)} templates (count, last, service, level):"]

    for row in rows:
        example = json.loads(row["example"]) if row.get("example") else {}
        level = row.get("severity_text") or "-"
        lines.append(f"{row['hits']:>5}  {time(row['last'])}  {row.get('service.name', '-')}  {level}  {row['template']}")

        if example.get("_msg") and example["_msg"] != row["template"]:
            lines.append(f"       e.g. {example['_msg']}")

        lines += [f"   {line}" for line in exception(example, stack=False)]

        if example.get("trace_id"):
            lines.append(f"       trace={example['trace_id']}")

    return lines
