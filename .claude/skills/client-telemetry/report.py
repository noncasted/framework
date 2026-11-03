# The text an agent reads: one line per session, tables of metrics and windows, groups of errors. Hidden samples stay out.
import re
from datetime import datetime

EXPECTED = ("frame.fps frame.dt.avg frame.dt.p50 frame.dt.p95 frame.dt.max frame.over33 frame.over50 frame.cpu frame.cpu.main "
            "frame.gpu memory.allocated memory.reserved memory.mono.used memory.mono.heap memory.textures gc.collections "
            "render.draw_calls render.setpass render.triangles render.vertices render.textures render.render_textures "
            "web.wasm.heap web.js.used web.js.total web.js.limit").split()
_SUMMED = ("frame.over33", "frame.over50", "gc.collections")
_STACK_LINES = 6


# The values are of the game: no list of them here. Old rows have none.
def tags_text(info):
    return ",".join(info["tags"]) or "-"


def session_line(info):
    device = info["device"]
    digest = info.get("digest")
    duration = f" {digest['durationMs'] / 1000:.0f}s" if digest else ""
    errors = f" errors={digest['errorCount']}" if digest else ""
    fps = _average(digest["summary"], "frame.fps") if digest else None
    fps_text = f" fps={number(fps, '')}" if fps is not None else ""

    return (f"{info['id']} {info['status']} tags={tags_text(info)} {info['startedAt']}{duration} {_known(device, 'platform')} {_known(device, 'buildVersion')} "
            f"{_known(device, 'browser')}{device.get('browserMajor') or ''} {_known(device, 'os')}{device.get('osMajor') or ''}{errors}{fps_text}")


def startup_line(info):
    return (f"{info['id']} {'final' if info['final'] else 'snapshot'} received={info['receivedAt']} name={info['name']} "
            f"platform={_known(info['device'], 'platform')} build={_known(info['device'], 'buildVersion')} duration={_seconds(info['durationMs'])} "
            f"frames={info['frames']} spans={info['spanCount']} depth={info['depth']} unfinished={info['unfinishedSpans']}")


# The own time decides, not the duration: a parent holds everything under it. Whether a long own time is a wait or a load is told by
# ms_per_frame next to it: the frames went on at the rate of the game during a wait.
def startup_lines(page, top):
    info = page["startup"]
    spans = page["spans"]
    longest = sorted(spans, key=lambda span: -span["ownMs"])[:top]
    unfinished = [span for span in spans if span["unfinished"]]
    lines = [f"startup {startup_line(info)}", device_line(info),
             f"limits dropped_spans={info['droppedSpans']} rejected={info['rejectedSpans']} cut_names={info['cutNames']}"]

    if not info["final"]:
        lines.append("snapshot: the trace was open when it was sent, its open spans go on past these numbers")

    lines.append(f"own_time top={len(longest)}")
    lines += table([["own", "duration", "frames", "ms_per_frame", "depth", "span"]] + [_span_row(span) for span in longest])
    lines.append(f"unfinished {len(unfinished)}")
    lines += [f"  {_seconds(span['durationMs'])} depth={span['depth']} {span['name']}" for span in unfinished]
    lines.append(f"sessions {len(page['sessions'])}")
    lines += [session_line(session) for session in page["sessions"]]

    return lines


# A client may send only a part of the device: what it did not send is "-", like tags=-.
def device_line(info):
    device = info["device"]
    gpu = repr(device["gpu"]) if device.get("gpu") else "-"
    screen = f"{device['screenWidth']}x{device['screenHeight']}@{_known(device, 'dpr')}" if device.get("screenWidth") else "-"

    return (f"device platform={_known(device, 'platform')} build={_known(device, 'buildVersion')} browser={_with_major(device, 'browser', 'browserMajor')} "
            f"os={_with_major(device, 'os', 'osMajor')} gpu={gpu} cores={_known(device, 'cores')} memoryGB={_known(device, 'memoryClass')} "
            f"screen={screen}")


def _known(device, field):
    return device.get(field) or "-"


def _with_major(device, field, major):
    return f"{device[field]} {device.get(major) or '-'}" if device.get(field) else "-"


def number(value, unit):
    if unit == "bytes":
        return f"{value / 1048576:.1f}MB"

    if abs(value) >= 1000:
        return str(round(value))

    return f"{value:.1f}".rstrip("0").rstrip(".")


def table(rows):
    widths = [max(len(row[column]) for row in rows) for column in range(len(rows[0]))]

    return ["  ".join(cell.ljust(width) for cell, width in zip(row, widths)).rstrip() for row in rows]


def metrics_lines(summary):
    rows = [["metric", "unit", "n", "avg", "p50", "p95", "min", "max", "growth"]]

    for metric in summary["metrics"]:
        unit = metric.get("unit") or ""
        cells = [number(metric[key], unit) for key in ("average", "p50", "p95", "min", "max")]
        growth = metric.get("growth")
        rows.append([metric["name"], unit or "-", str(metric["count"]), *cells, "-" if growth is None else number(growth, unit)])

    return table(rows)


def not_reported_line(summary):
    present = {metric["name"] for metric in summary["metrics"]}

    return "not_reported " + (" ".join(name for name in EXPECTED if name not in present) or "-")


def trend_lines(rows, units, windows):
    visible = [row for row in rows if not row["hidden"]]
    first, last = visible[0]["time"], visible[-1]["time"]
    count = min(windows, len(visible))
    width = (last - first) / count
    slices = [[] for _ in range(count)]

    for row in visible:
        index = min(count - 1, int((row["time"] - first) / width)) if width else 0
        slices[index].append(row)

    header = ["t_s", *(f"{(first + width * index) / 1000:.0f}-{(first + width * (index + 1)) / 1000:.0f}" for index in range(count))]
    table_rows = [header, ["samples", *(str(len(part)) for part in slices)]]

    for name in sorted({name for row in visible for name in row["values"]}):
        table_rows.append([name, *(_window_cell(part, name, units.get(name, "")) for part in slices)])

    return ["trend (over*, gc.collections: sum; *.max: max; bytes: last value; the rest: average)", *table(table_rows)]


def group_rows(rows):
    groups = {}

    for row in rows:
        group = groups.setdefault(row["fingerprint"], {"fingerprint": row["fingerprint"], "level": row["level"], "message": row["message"],
                                                        "count": 0, "rows": 0, "firstSeenAt": row["time"], "lastSeenAt": row["time"]})
        group["count"] += row["repeat"]
        group["rows"] += 1
        group["lastSeenAt"] = row["time"]

    return sorted(groups.values(), key=lambda group: -group["count"])


def error_group_lines(groups, stacks, started):
    lines = []

    for group in groups:
        lines.append(f"{group['fingerprint']} {group['level']} count={group['count']} rows={group['rows']} "
                     f"first={_moment(group['firstSeenAt'], started)} last={_moment(group['lastSeenAt'], started)} {group['message']}")

        for line in (stacks or {}).get(group["fingerprint"], "").splitlines()[:_STACK_LINES]:
            lines.append(f"    {line}")

    return lines


def global_error_lines(answer):
    lines = [f"groups={answer['totalGroups']} counted_sessions={answer['countedSessions']} sessions_without_digest={answer['sessionsWithoutDigest']} "
             f"omitted_groups={answer['omittedGroups']} omitted_count={answer['omittedCount']} dropped_errors={answer['droppedErrors']}"]

    for group in answer["groups"]:
        lines.append(f"{group['fingerprint']} {group['level']} count={group['count']} sessions={group['sessions']} "
                     f"last={group['lastSeenAt']} {group['message']}")
        lines.append(f"    in {' '.join(group['sessionIds'])}")

    return lines


# Since the start of the session, to compare with the windows of the trend: the t of a row, or the time a digest took a row at.
def _span_row(span):
    per_frame = f"{span['durationMs'] / span['frames']:.1f}" if span["frames"] else "-"

    return [_seconds(span["ownMs"]), _seconds(span["durationMs"]), str(span["frames"]), per_frame, str(span["depth"]), span["name"]]


def _seconds(milliseconds):
    return f"{milliseconds / 1000:.2f}s" if milliseconds >= 1000 else f"{milliseconds:.0f}ms"


def _moment(value, started):
    milliseconds = value if isinstance(value, (int, float)) else (_instant(value) - _instant(started)).total_seconds() * 1000

    return f"{milliseconds / 1000:.1f}s"


def _instant(text):
    return datetime.fromisoformat(re.sub(r"(\.\d{6})\d*", r"\1", text).replace("Z", "+00:00"))


def _average(summary, name):
    return next((metric["average"] for metric in summary["metrics"] if metric["name"] == name), None)


def _window_cell(rows, name, unit):
    values = [row["values"][name] for row in rows if name in row["values"]]

    if not values:
        return "-"

    if name in _SUMMED:
        value = sum(values)
    elif name.endswith(".max"):
        value = max(values)
    elif unit == "bytes":
        value = values[-1]
    else:
        value = sum(values) / len(values)

    return number(value, unit)
