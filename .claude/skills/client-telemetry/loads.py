# The loads of assets in a session, from its marks load.*: what failed, what was slow, what never ended, the longest that ended.
from text import seconds

_PREFIX = "load."
_LONGEST = 5


def is_load(mark):
    return mark["name"].startswith(_PREFIX)


def load_lines(marks):
    loads = [mark for mark in marks if is_load(mark)]

    if not loads:
        return ["loads none"]

    def of(name):
        return [mark for mark in loads if mark["name"] == _PREFIX + name]

    ended = sorted(of("end"), key=lambda mark: -(mark.get("durationMs") or 0))
    lines = [f"loads started={len(of('start'))} ended={len(ended)} failed={len(of('failed'))} slow={len(of('slow'))} "
             f"unfinished={len(of('unfinished'))} (a load begun before the session has no start)"]

    lines += [f"  failed {_moment(mark)} {_detail(mark)} after {_duration(mark)}" for mark in of("failed")]
    lines += [f"  slow {_moment(mark)} {_detail(mark)} over {_duration(mark)}" for mark in of("slow")]
    lines += [f"  unfinished {_moment(mark)} {_detail(mark)} {_duration(mark)} so far" for mark in of("unfinished")]

    if ended:
        lines.append("  longest " + ", ".join(f"{_duration(mark)} {_detail(mark)}" for mark in ended[:_LONGEST]))

    return lines


def _moment(mark):
    return f"{mark['time'] / 1000:.1f}s"


def _detail(mark):
    return mark.get("detail") or "-"


def _duration(mark):
    return seconds(mark["durationMs"]) if mark.get("durationMs") is not None else "-"
