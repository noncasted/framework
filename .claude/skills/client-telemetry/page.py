# The page before the build in a startup trace of the web (the root span Page): the navigation of the document, its parsing, the
# files with their sizes and cache, the gap from the last file to the start of the build and the spans of the template of the game.
from text import seconds, table

_ROOT = "Page"
_NAVIGATION_STAGES = ("Redirect", "DNS", "Connect", "Request", "Response")
_DOCUMENT_STAGES = ("Document", "Load event")


def page_lines(spans):
    root = next((span for span in spans if span["parentIndex"] == -1 and span["name"] == _ROOT), None)

    if root is None:
        return ["page none: the trace begins at the start of the build (not the web, or a client before the page trace)"]

    children = _children(spans, root["index"])
    build = next((span for span in spans if span["parentIndex"] == -1 and span is not root), None)
    build_start = build["startMs"] if build else root["durationMs"]
    lines = [f"page {seconds(root['durationMs'])} until the build started at {seconds(build_start)}"]

    lines += _navigation_lines(spans, children)
    lines += _file_lines(spans, children)
    lines += [f"  build_start {seconds(span['durationMs'])} from the last file to the build (wasm compilation, the start of the instance)"
              for span in children if span["name"] == "Build start"]
    lines += _template_lines(spans, children)

    return lines


def _navigation_lines(spans, children):
    lines = []

    for span in children:
        if span["name"] == "Navigation":
            stages = " ".join(f"{stage['name']}={seconds(stage['durationMs'])}" for stage in _children(spans, span["index"])
                              if stage["name"] in _NAVIGATION_STAGES)
            lines.append(f"  navigation {seconds(span['durationMs'])}: {stages or '-'}")

    document = [f"{span['name']} {seconds(span['durationMs'])}" for span in children if span["name"] in _DOCUMENT_STAGES]

    if document:
        lines.append("  " + " ".join(document) + " (from the end of the response; the scripts of the head run here)")

    return lines


def _file_lines(spans, children):
    group = next((span for span in children if span["name"] == "Files"), None)

    if group is None:
        return ["  files none"]

    files = _children(spans, group["index"])
    network = sum(file["transferBytes"] for file in files)
    cached = sum(1 for file in files if file["cached"])
    unknown = sum(1 for file in files if not file["cached"] and file["transferBytes"] == 0)
    rows = [["duration", "start", "network", "cached", "file"]]
    rows += [[seconds(file["durationMs"]), seconds(file["startMs"]), _size(file), "yes" if file["cached"] else ("-" if not file["transferBytes"] else "no"),
              file["file"] or "-"] for file in sorted(files, key=lambda file: -file["durationMs"])]

    return ([f"  files {len(files)} network={network / 1048576:.1f}MB cached={cached} unknown_size={unknown} "
             f"(unknown: another origin without Timing-Allow-Origin)"]
            + ["    " + line for line in table(rows)])


def _template_lines(spans, children):
    known = {"Navigation", "Files", "Build start", *_DOCUMENT_STAGES}
    roots = [span for span in children if span["name"] not in known]
    lines = [f"  template {len(roots)}"]

    def add(span, depth):
        unfinished = " (unfinished)" if span["unfinished"] else ""
        lines.append(f"    {'  ' * depth}{seconds(span['durationMs'])} at {seconds(span['startMs'])} {span['name']}{unfinished}")

        for child in _children(spans, span["index"]):
            add(child, depth + 1)

    for root in roots:
        add(root, 0)

    return lines


def _children(spans, index):
    return [span for span in spans if span["parentIndex"] == index]


def _size(file):
    return f"{file['transferBytes'] / 1048576:.1f}MB" if file["transferBytes"] else "-"
