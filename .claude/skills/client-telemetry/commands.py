# The subcommands. A report prints what the backend holds and leaves the conclusions to the agent (SKILL.md).
import math
import time

import report
from loads import is_load, load_lines
from page import page_lines
from common import WAIT_TIMEOUT, Failure

_MAX_WAIT_SECONDS = 30
_NEW_PAGE = 100


def sessions(api, arguments):
    items = api.sessions(status=arguments.status, platform=arguments.platform, build=arguments.build, tag=arguments.tag, metric=arguments.metric,
                         averageBelow=arguments.average_below, averageAbove=arguments.average_above, minErrors=arguments.min_errors,
                         offset=arguments.offset, limit=arguments.limit)

    print(f"sessions={len(items)} (newest first, limit {arguments.limit})")

    for info in items:
        print(report.session_line(info))


def summary(api, arguments):
    print_report(api, api.session(arguments.id), arguments.windows)


def errors(api, arguments):
    if not arguments.id:
        print("\n".join(report.global_error_lines(api.error_groups(arguments.limit))))
        return

    info = api.session(arguments.id)
    rows = api.errors(arguments.id)
    stacks = {}

    for row in rows:
        stacks.setdefault(row["fingerprint"], row["stack"])

    print_error_groups(info, rows, stacks)


def watch(api, arguments):
    info = api.session(arguments.id)
    after = info["ackSeq"] if info["status"] == "live" else 0

    print(f"watch {arguments.id} status={info['status']} tags={report.tags_text(info)} seconds={arguments.seconds}", flush=True)
    _observe_and_report(api, arguments.id, after, arguments.seconds, arguments.windows)


def monitor(api, arguments):
    # The backend filters, so the baseline holds only sessions of this filter: one with another tag plays no part.
    filters = {"platform": arguments.platform, "build": arguments.build, "tag": arguments.tag}
    baseline = {info["id"] for info in api.all_sessions(**filters)}

    print(f"baseline={len(baseline)} waiting for a new session{_tags_clause(arguments)}, up to {arguments.timeout:g}s", flush=True)

    chosen = _wait_new(api, arguments, filters, baseline)

    print(f"new {report.session_line(chosen)}", flush=True)
    _observe_and_report(api, chosen["id"], 0, arguments.seconds, arguments.windows)

    for info in _new_sessions(api, arguments, filters, baseline)[0]:
        if info["id"] != chosen["id"]:
            print(f"other_new {report.session_line(info)}")


def startups(api, arguments):
    items = api.startups(arguments.limit)

    print(f"startups={len(items)} (newest first, limit {arguments.limit})")

    for info in items:
        print(report.startup_line(info))


def startup(api, arguments):
    page = api.startup(arguments.id)

    print("\n".join(report.startup_lines(page, arguments.top)))


def print_report(api, info, windows, first=None):
    rows_summary = api.summary(info["id"], first)
    digest = info.get("digest")

    print(f"session {info['id']} status={info['status']} tags={report.tags_text(info)} started={info['startedAt']} closed={info.get('closedAt')} "
          f"startup={info['startupId']} samples={rows_summary['samples']} hidden={rows_summary['hiddenSamples']}")
    print(report.device_line(info))

    if digest:
        print(f"digest duration={digest['durationMs'] / 1000:.0f}s errors={digest['errorCount']} error_rows={digest['errorRows']} marks={digest['marks']} "
              f"dropped_samples={digest['droppedSamples']} dropped_errors={digest['droppedErrors']} dropped_marks={digest['droppedMarks']} "
              f"rejected_requests={digest['rejectedRequests']}")
    elif info["status"] != "live":
        print("digest none: the session closed without its data, a crash of the silo")

    print(report.not_reported_line(rows_summary))

    if rows_summary["samples"] == 0:
        print("no samples outside the background")
    else:
        rows = [row for row in api.samples(info["id"]) if first is None or row["time"] >= first]
        units = {metric["name"]: metric.get("unit") or "" for metric in rows_summary["metrics"]}

        print("\n".join(report.metrics_lines(rows_summary)))
        print("\n".join(report.trend_lines(rows, units, windows)))

    marks = [mark for mark in api.marks(info["id"]) if first is None or mark["time"] >= first]

    plain = [mark for mark in marks if not is_load(mark)]

    if plain:
        print("marks " + " ".join(f"{mark['time'] / 1000:.1f}s={mark['name']}" for mark in plain))

    print("\n".join(load_lines(marks)))

    rows = [row for row in api.errors(info["id"]) if first is None or row["time"] >= first]

    print_error_groups(info, rows, {}, whole=first is None)


def print_error_groups(info, rows, stacks, whole=True):
    digest = info.get("digest") if whole else None

    if digest:
        answer = digest["errorGroups"]
        groups = answer["groups"]
        print(f"errors groups={len(groups)} omitted_groups={answer['omittedGroups']} omitted_count={answer['omittedCount']}")
    else:
        groups = report.group_rows(rows)
        print(f"errors groups={len(groups)}")

    lines = report.error_group_lines(groups, stacks, info["startedAt"])

    if lines:
        print("\n".join(lines))


def _observe_and_report(api, session_id, after, seconds, windows):
    start = after
    reason, after = _observe(api, session_id, after, seconds)

    print(f"end reason={reason}")

    if reason == "lost":
        print("lost: the socket broke, or the gateway died and the timer of the server closed it; this id stays closed")

    info = api.session(session_id)

    if start == 0:
        print_report(api, info, windows)
        return

    rows = api.samples(session_id, start)

    if not rows:
        print(f"no samples during the watch; the whole session: summary {session_id}")
        return

    print_report(api, info, windows, rows[0]["time"])
    print(f"this covers the watch only; the whole session: summary {session_id}")


def _observe(api, session_id, after, seconds):
    deadline = time.monotonic() + seconds

    while True:
        remaining = deadline - time.monotonic()

        if remaining <= 0:
            return "seconds", after

        page = api.wait_samples(session_id, after, min(_MAX_WAIT_SECONDS, max(1, math.ceil(remaining))))
        after = page["lastSeq"]

        if page["status"] != "live":
            return page["status"], after


def _wait_new(api, arguments, filters, baseline):
    deadline = time.monotonic() + arguments.timeout
    skipped = 0

    while True:
        found, skipped = _new_sessions(api, arguments, filters, baseline)

        if found:
            # The newest of the live ones: a closed one has nothing more to watch, its id is printed as other_new.
            return sorted(found, key=lambda info: info["status"] != "live")[0]

        if time.monotonic() >= deadline:
            raise Failure(WAIT_TIMEOUT, _no_session_message(arguments, len(baseline), skipped))

        time.sleep(arguments.poll)


# New is an id out of the baseline, whatever the clock of a device says. The list is newest first by the time of the backend, so
# the next page is needed only when a whole page is new.
def _new_sessions(api, arguments, filters, baseline):
    found = []
    offset = 0

    while True:
        page = api.sessions(**filters, offset=offset, limit=_NEW_PAGE)
        fresh = [info for info in page if info["id"] not in baseline]
        found += fresh
        offset += len(page)

        if len(page) < _NEW_PAGE or len(fresh) < len(page):
            break

    matching = [info for info in found if not arguments.os or info["device"].get("os") == arguments.os]

    return matching, len(found) - len(matching)


def _tags_clause(arguments):
    return f" with {' '.join(f'--tag {tag}' for tag in arguments.tag)}" if arguments.tag else ""


def _no_session_message(arguments, baseline, skipped):
    filters = f" {skipped} new sessions did not match --os {arguments.os}." if skipped else ""
    tags = (f" Sessions of other tags do not count: has the game reached the part that sets this tag, and is the value the one of the game "
            f"(the command sessions shows the tags)?") if arguments.tag else ""

    return (f"no new session{_tags_clause(arguments)} in {arguments.timeout:g}s (baseline {baseline} sessions).{filters}{tags} A session is a part of "
            f"the game that the game opens. Check: the flag of the client telemetry on the Features page of the console (off: no sessions at all); "
            f"the game opens a session, the flag alone gives nothing; that the client plays against this address; the filters --platform, --build, --os, --tag.")
