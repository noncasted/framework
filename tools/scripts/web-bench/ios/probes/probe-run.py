#!/usr/bin/env python3
"""Runs one GL probe in Safari of an iPhone / iPad: a fresh Safari (safari-restart.sh) on the production page of the
game, the probe injected once the game is ready, then every <interval> s a screenshot and a progress line, the result
of the probe at the end.

Probes (files next to this one):
  probe  general counts a second, texture events, context loss          window.__p
  frame  every GL call of one frame at 8 ... 55 s                       window.__f.frames
  buf    vertex / index buffers read back vs shadow copies              window.__b.res
  tex    RGBA8 texture uploads read back vs the uploaded bytes          window.__t.res
  uni    all uniforms of every used program at 4 ... 66 s (uni-scan.py) window.__u.res
The uni probe found the broken glstate_matrix_projection[3] of iPadOS 18.2 (see the header of uni.js).

Usage: probe-run.py --device <name> --probe <probe> --out <dir> [--query '<query>'] [--autorun] [--url <url>]
                    [--samples N] [--interval S] [--ready '<js>'] [--project <game dir>]
  --url       page to open instead of https://<host>/?r=<random><query> (host from .claude/device-lab.json)
  --autorun   prepend autorunQuery of .claude/device-lab.json (a run instead of the menu)
  --samples   screenshots / progress lines (default: enough to cover the last check of the probe)
  --interval  seconds between samples after the screenshot (default 3; a screenshot itself takes 3-6 s)
  --ready     expression that is truthy once the game made its GL context (default !!window.unityInstance)
Output in <dir>: probe.json (the result as JSON), progress.txt, console.txt, shot_<HHMMSS>.png + .small.png (20%)
Environment: DEVICE_LAB_DEVICES, DEVICE_LAB_HOME (see ../devicelab.py)
Example:
  probe-run.py --device ipad7 --probe uni --out /tmp/uni1 --project /projects/pixel-swarm && uni-scan.py /tmp/uni1/probe.json
"""
import argparse
import json
import os
import random
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
IOS = HERE.parent
sys.path.insert(0, str(IOS))
import devicelab  # noqa: E402

# file, progress expression, result expression, default samples
PROBES = {
    "probe": ("probe.js", "{ev: window.__p.ev.length, lost: window.__p.lost}", "window.__p", 18),
    "frame": ("frame.js", "{frames: Object.keys(window.__f.frames)}", "window.__f.frames", 13),
    "buf": ("buf.js", "{res: window.__b.res}", "window.__b.res", 9),
    "tex": ("tex.js", "{res: window.__t.res}", "window.__t.res", 13),
    "uni": ("uni.js", "{dumps: Object.keys(window.__u.res)}", "window.__u.res", 15),
}


class RunError(Exception):
    pass


def run(command, **kwargs):
    result = subprocess.run(command, capture_output=True, text=True, **kwargs)
    if result.returncode != 0:
        raise RunError(f"{' '.join(command[:4])} ...: exit {result.returncode}: {result.stderr.strip()}")
    return result.stdout.strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--device", required=True)
    parser.add_argument("--probe", required=True, choices=sorted(PROBES))
    parser.add_argument("--out", required=True)
    parser.add_argument("--query", default="")
    parser.add_argument("--autorun", action="store_true")
    parser.add_argument("--url")
    parser.add_argument("--samples", type=int)
    parser.add_argument("--interval", type=float, default=3)
    parser.add_argument("--ready", default="!!window.unityInstance")
    parser.add_argument("--project", default=os.getcwd())
    args = parser.parse_args()

    devicelab.ios_device(args.device)
    python = str(devicelab.pmd_python())
    file, progress, result, default_samples = PROBES[args.probe]
    samples = args.samples or default_samples
    if args.url:
        url = args.url
    else:
        host = devicelab.game_value("host", args.project)
        parts = []
        if args.autorun:
            parts.append(devicelab.game_value("autorunQuery", args.project).lstrip("?"))
        parts.append(f"r={random.randint(0, 99999)}")
        if args.query.lstrip("?&"):
            parts.append(args.query.lstrip("?&"))
        url = f"https://{host}/?" + "&".join(parts)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    def ctl(*command, **kwargs):
        return run([python, str(IOS / "iosctl.py"), "--device", args.device, *command], **kwargs)

    def shot():
        stamp = time.strftime("%H%M%S")
        run([str(IOS / "screenshot.sh"), "--device", args.device, "--resize", "20", str(out / f"shot_{stamp}.png")])
        return stamp

    run([str(IOS / "safari-restart.sh"), "--device", args.device, url])
    print(f"opened {time.strftime('%T')} {url}", flush=True)
    # The console is read for the whole run and stopped by its pid at the end.
    with open(out / "console.txt", "w") as console_file:
        console = subprocess.Popen([python, str(IOS / "iosctl.py"), "--device", args.device, "console", "100000"],
                                   stdout=console_file, stderr=subprocess.STDOUT)
    try:
        ctl("--timeout", "120", "wait", args.ready)
        print(f"ready {time.strftime('%T')}", flush=True)
        answer = ctl("eval", (HERE / file).read_text())
        if not answer.startswith("ok"):
            raise RunError(f"{file} answered '{answer}'")
        print(f"injected {file}: {answer}", flush=True)
        with open(out / "progress.txt", "w") as progress_file:
            for _ in range(samples):
                stamp = shot()
                line = ctl("eval", f"JSON.stringify(Object.assign({{t: Math.round(performance.now())}}, {progress}))")
                progress_file.write(f"{stamp} {line}\n")
                progress_file.flush()
                time.sleep(args.interval)
        value = json.loads(ctl("eval", f"JSON.stringify({result})"))
        with open(out / "probe.json", "w") as f:
            json.dump(value, f)
    finally:
        console.terminate()
        console.wait(10)
    print(f"end {time.strftime('%T')} {out / 'probe.json'}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except devicelab.ConfigError as e:
        print(f"error: {e}", file=sys.stderr)
        sys.exit(2)
    except RunError as e:
        print(f"error: {e}", file=sys.stderr)
        sys.exit(1)
