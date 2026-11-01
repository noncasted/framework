#!/usr/bin/env python3
"""Compose visual variants into one labelled contact sheet (PNG) and, when any variant is animated, a synced GIF.

Each variant is a PNG, a GIF, or a folder of frame PNGs (sorted by name). Pixel art is upscaled with point
filtering. Labels are "A", "B", ... plus an optional caption.

Usage:
  compose.py --out docs/media/research/<topic>/<topic>_variants --cols 3 \
      --title "Upgrade card back" "A=v01.png:double frame" "B=v02.png:arch head" "C=frames_c/:pulse"

Writes <out>.png (first frame of every variant) and <out>.gif (only when a variant has more than one frame).
Requires ImageMagick 7 (magick).
"""
import argparse
import shutil
import string
import subprocess
import sys
import tempfile
from pathlib import Path

TILE_TARGET = 384
BACKGROUND = "#1e1e24"
FOREGROUND = "#e8e8e8"


def font():
    result = subprocess.run(["fc-match", "-f", "%{file}", "monospace"], capture_output=True, text=True)
    if result.returncode != 0 or not result.stdout:
        raise SystemExit("fc-match found no monospace font")
    return result.stdout


def magick(*args):
    subprocess.run(["magick", *map(str, args)], check=True)


def parse_variant(index, text):
    label = string.ascii_uppercase[index] if index < 26 else str(index + 1)
    if "=" in text:
        label, text = text.split("=", 1)
    path, _, caption = text.partition(":")
    return label, Path(path), caption


def frames_of(path, work):
    if path.is_dir():
        frames = sorted(p for p in path.iterdir() if p.suffix.lower() == ".png")
        if not frames:
            raise SystemExit(f"no png frames in {path}")
        return frames
    if not path.exists():
        raise SystemExit(f"variant not found: {path}")
    if path.suffix.lower() == ".gif":
        target = work / f"{path.stem}_%03d.png"
        magick(path, "-coalesce", target)
        return sorted(work.glob(f"{path.stem}_*.png"))
    return [path]


def tile(frame, label, caption, scale, size, font_file, out):
    text = f"{label}  {caption}" if caption else label
    magick(frame, "-filter", "point", "-resize", f"{scale * 100}%",
           "-background", "none", "-gravity", "center", "-extent", size,
           "-background", BACKGROUND, "-flatten",
           "-fill", FOREGROUND, "-font", font_file, "-pointsize", 18,
           "-gravity", "north", "-splice", "0x30", "-annotate", "+0+5", text, "+repage", out)


def sheet(tiles, cols, title, font_file, out):
    rows = [tiles[i:i + cols] for i in range(0, len(tiles), cols)]
    with tempfile.TemporaryDirectory() as tmp:
        row_files = []
        for r, row in enumerate(rows):
            row_file = Path(tmp) / f"row_{r}.png"
            magick(*row, "-background", BACKGROUND, "-splice", "12x0", "+append", "-chop", "12x0", "+repage", row_file)
            row_files.append(row_file)
        args = [*row_files, "-background", BACKGROUND, "-splice", "0x12", "-append", "-chop", "0x12",
                "-bordercolor", BACKGROUND, "-border", 16]
        if title:
            args += ["-fill", FOREGROUND, "-font", font_file, "-pointsize", 22,
                     "-gravity", "north", "-splice", "0x40", "-annotate", "+0+12", title]
        magick(*args, "+repage", out)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True, help="output path without extension")
    parser.add_argument("--scale", type=int, default=0, help=f"0 = largest integer keeping a tile within {TILE_TARGET} px")
    parser.add_argument("--cols", type=int, default=0, help="0 = ceil(sqrt(n))")
    parser.add_argument("--title", default="")
    parser.add_argument("--delay", type=int, default=10, help="gif frame delay, 1/100 s")
    parser.add_argument("variants", nargs="+", help="[LABEL=]path[:caption]")
    args = parser.parse_args()

    if not shutil.which("magick"):
        raise SystemExit("ImageMagick 7 (magick) is required")

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    count = len(args.variants)
    cols = args.cols or next(c for c in range(1, count + 1) if c * c >= count)

    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        variants = []
        for i, text in enumerate(args.variants):
            label, path, caption = parse_variant(i, text)
            sub = work / f"src_{i}"
            sub.mkdir()
            variants.append((label, caption, frames_of(path, sub)))

        dims = subprocess.run(["magick", "identify", "-format", "%w %h\n",
                               *[v[2][0] for v in variants]], check=True, capture_output=True, text=True)
        sizes = [tuple(map(int, line.split())) for line in dims.stdout.split("\n") if line]
        largest = max(max(w, h) for w, h in sizes)
        scale = args.scale or max(1, TILE_TARGET // largest)
        font_file = font()
        width = max(w for w, _ in sizes) * scale + 16
        height = max(h for _, h in sizes) * scale + 16
        size = f"{width}x{height}"

        frame_count = max(len(v[2]) for v in variants)
        sheets = []
        for f in range(frame_count):
            tiles = []
            for i, (label, caption, frames) in enumerate(variants):
                tile_file = work / f"tile_{f}_{i}.png"
                tile(frames[f % len(frames)], label, caption, scale, size, font_file, tile_file)
                tiles.append(tile_file)
            sheet_file = work / f"sheet_{f:03d}.png"
            sheet(tiles, cols, args.title, font_file, sheet_file)
            sheets.append(sheet_file)

        shutil.copy(sheets[0], out.with_suffix(".png"))
        print(out.with_suffix(".png"))
        if frame_count > 1:
            magick("-delay", args.delay, "-loop", 0, *sheets, "-layers", "Optimize", out.with_suffix(".gif"))
            print(out.with_suffix(".gif"))


if __name__ == "__main__":
    sys.exit(main())
