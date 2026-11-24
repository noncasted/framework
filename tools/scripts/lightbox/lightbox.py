#!/usr/bin/env python3
"""Lightbox (Лайтбокс): the image viewer and visual picker of a Claude Code session, one small GTK4 + WebKit window per
terminal.

  lightbox.py show --from TEXT <path>...          add images to the viewer, each captioned with its origin
  lightbox.py show --hook                         read a PostToolUse(Read) JSON from stdin
  lightbox.py pick --title T --from TEXT [--delay MS] "A=<path>:<caption>" ...
                                                  show a grid of variants, block until the user answers, print JSON
  lightbox.py app ...                             the window itself (started by the commands above)
  lightbox.py debug eval <js> | snapshot <png>    drive the window from a script (checks)

A variant is a PNG / JPEG / GIF / WebP / SVG or a folder of frame PNGs (played at --delay ms).
The pick prints {"selected": ["B", "D"], "note": "..."} and exits 0; a cancelled pick prints
{"cancelled": "<reason>"} and exits 3.

The window lives on the workspace of the terminal that runs the session, takes no focus, and quits when the terminal
does. Every image is snapshotted into $XDG_RUNTIME_DIR/lightbox-<terminal pid>/ when shown, so later edits of the
file do not change what the history shows.

Files: session.py finds the terminal and talks to its window (starting it when needed), window.py is the window, ui/ the
page. install.sh links lightbox-show and lightbox-pick into ~/.local/bin.
"""
import argparse
import json
import string
import sys
from pathlib import Path

from session import request

IMAGE_SUFFIXES = {".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg", ".tif", ".tiff"}
EXIT_CANCELLED = 3


def image_path(text):
    path = Path(text).expanduser()
    if not path.exists():
        raise SystemExit(f"lightbox: no such file: {text}")
    path = path.resolve()
    if path.is_dir():
        frames = sorted(p for p in path.iterdir() if p.suffix.lower() == ".png")
        if not frames:
            raise SystemExit(f"lightbox: no png frames in {text}")
    elif path.suffix.lower() not in IMAGE_SUFFIXES:
        raise SystemExit(f"lightbox: not an image: {text}")
    return str(path)


def command_show(args):
    if args.hook:
        payload = json.load(sys.stdin)
        # Subagents read images for their own checks; only the main session pops up.
        if payload.get("agent_id"):
            return
        path = payload.get("tool_input", {}).get("file_path", "")
        if Path(path).suffix.lower() not in IMAGE_SUFFIXES or not Path(path).is_file():
            return
        items = [{"path": str(Path(path).resolve()), "from": "Read tool (Claude looked at the file)"}]
    else:
        if not args.paths:
            raise SystemExit("usage: lightbox-show --from '<where the image came from>' <path>...")
        if not args.source:
            raise SystemExit("lightbox-show: --from is required: say where the image came from "
                             "(the tool, scene, script or URL that produced it), e.g. "
                             "--from 'Unity Play, scene Arena, camera Main'")
        items = [{"path": image_path(p), "from": args.source} for p in args.paths]
        for item in items:
            if Path(item["path"]).is_dir():
                raise SystemExit("lightbox-show: a folder of frames is a variant of lightbox-pick, "
                                 "show a GIF instead")
    answer = request({"cmd": "show", "items": items})
    if "error" in answer:
        raise SystemExit(f"lightbox-show: {answer['error']}")


def parse_variant(index, text):
    label = string.ascii_uppercase[index] if index < 26 else str(index + 1)
    if "=" in text.split(":", 1)[0]:
        label, text = text.split("=", 1)
    path, _, caption = text.partition(":")
    return {"label": label, "path": image_path(path), "caption": caption}


def command_pick(args):
    if not args.source:
        raise SystemExit("lightbox-pick: --from is required: where the variants came from "
                         "(the generator, render setup or script)")
    items = [parse_variant(i, v) for i, v in enumerate(args.variants)]
    labels = [item["label"] for item in items]
    if len(set(labels)) != len(labels):
        raise SystemExit(f"lightbox-pick: duplicate labels: {labels}")
    answer = request({"cmd": "pick", "title": args.title, "from": args.source, "delay": args.delay, "items": items})
    if "error" in answer:
        raise SystemExit(f"lightbox-pick: {answer['error']}")
    print(json.dumps(answer, ensure_ascii=False))
    if "cancelled" in answer:
        sys.exit(EXIT_CANCELLED)


def command_debug(args):
    if args.action == "eval":
        answer = request({"cmd": "eval", "script": args.value})
    else:
        answer = request({"cmd": "snapshot", "path": str(Path(args.value).resolve())})
    if "error" in answer:
        raise SystemExit(f"lightbox: {answer['error']}")


def command_app(args):
    from window import App
    App(args).run()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)

    show = commands.add_parser("show")
    show.add_argument("--from", dest="source", help="where the images came from")
    show.add_argument("--hook", action="store_true", help="read a PostToolUse(Read) JSON from stdin")
    show.add_argument("paths", nargs="*")
    show.set_defaults(handler=command_show)

    pick = commands.add_parser("pick")
    pick.add_argument("--title", required=True, help="what is being chosen")
    pick.add_argument("--from", dest="source", help="where the variants came from")
    pick.add_argument("--delay", type=int, default=100, help="ms per frame of a folder of frames")
    pick.add_argument("variants", nargs="+", help='"A=<path>:<caption>" (the label is optional: A, B, C... by order)')
    pick.set_defaults(handler=command_pick)

    debug = commands.add_parser("debug")
    debug.add_argument("action", choices=["eval", "snapshot"])
    debug.add_argument("value")
    debug.set_defaults(handler=command_debug)

    app = commands.add_parser("app")
    app.add_argument("--app-id", required=True)
    app.add_argument("--socket", required=True)
    app.add_argument("--cache", required=True)
    app.add_argument("--monitor-size", required=True)
    app.add_argument("--terminal-pid", type=int)
    app.set_defaults(handler=command_app)

    args = parser.parse_args()
    args.handler(args)


if __name__ == "__main__":
    main()
