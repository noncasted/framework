#!/usr/bin/env python3
"""Cuts the remote log of lan/serve.py into equal time windows of a run and prints the frame numbers of each window.

Usage: windows.py <log> [--start HH:MM:SS] [--secs 330] [--n 6] [--client <ip>] [--mark <text>]

  <log>      the log file of serve.py ("HH:MM:SS <client ip> <ms> <kind> <text>" per line)
  --start    the start of the first window, the time of the first line by default
  --secs     the length of the whole run in seconds (330)
  --n        the number of windows (6)
  --client   only the lines of this phone (its LAN ip); every client by default
  --mark     a text of the log (for example the first line the game writes when a run starts): prints its first time

Rows (a "-" where a window has no such line):
  raf, rafmax, raffps, long  the "frames" windows of remote-log.js: mean frame ms, the worst frame, fps, frames > 50 ms
  frame, fmax, fps           a console line "Frame: X avg, Y max/s (Z fps)" of the game (a latency overlay), if any
  dc, dcmax                  a console line "dc frames=N avg=X max=Y" (a draw counter script), if any
Example: windows.py /tmp/bench/log.txt --start 14:02:10 --client 192.168.0.147 --mark "Session seed"
"""
import argparse
import re
import sys

ROWS = [('raf', 'avg'), ('rafmax', 'max'), ('raffps', 'avg'), ('long', 'sum'),
        ('frame', 'avg'), ('fmax', 'max'), ('fps', 'avg'), ('dc', 'avg'), ('dcmax', 'max')]
RAF = re.compile(r' frames n=(\d+) avg=([\d.]+) max=(\d+) fps=(\d+) long=(\d+)')
LATENCY = re.compile(r'Frame: ([\d,.]+) avg, (\d+) max/s \((\d+) fps\)')
DRAWS = re.compile(r'dc frames=(\d+) avg=([\d.]+) max=(\d+)')
TIME = re.compile(r'^(\d\d):(\d\d):(\d\d) (\S+) ')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('log')
    parser.add_argument('--start')
    parser.add_argument('--secs', type=int, default=330)
    parser.add_argument('--n', type=int, default=6)
    parser.add_argument('--client')
    parser.add_argument('--mark')
    args = parser.parse_args()

    lines = []
    for raw in open(args.log, errors='replace'):
        m = TIME.match(raw)
        if not m:
            sys.exit(f'windows.py: not a line of serve.py: {raw[:120]!r}')
        if args.client and m.group(4) != args.client:
            continue
        lines.append((int(m.group(1)) * 3600 + int(m.group(2)) * 60 + int(m.group(3)), raw))
    if not lines:
        sys.exit('windows.py: no lines' + (f' of {args.client}' if args.client else ''))

    if args.start:
        h, mi, s = map(int, args.start.split(':'))
        s0 = h * 3600 + mi * 60 + s
    else:
        s0 = lines[0][0]
    width = args.secs / args.n
    rows = [{key: [] for key, _ in ROWS} for _ in range(args.n)]
    mark = None
    for t, line in lines:
        t -= s0
        if t < 0 or t >= args.secs:
            continue
        if args.mark and mark is None and args.mark in line:
            mark = t
        r = rows[int(t // width)]
        m = RAF.search(line)
        if m and int(m.group(1)) > 0:
            r['raf'].append(float(m.group(2))); r['rafmax'].append(int(m.group(3)))
            r['raffps'].append(int(m.group(4))); r['long'].append(int(m.group(5)))
        m = LATENCY.search(line)
        if m:
            r['frame'].append(float(m.group(1).replace(',', '.'))); r['fmax'].append(int(m.group(2)))
            r['fps'].append(int(m.group(3)))
        m = DRAWS.search(line)
        if m and int(m.group(1)) > 0:
            r['dc'].append(float(m.group(2))); r['dcmax'].append(int(m.group(3)))

    fold = {'avg': lambda v: f'{sum(v) / len(v):.1f}', 'max': lambda v: str(max(v)), 'sum': lambda v: str(sum(v))}
    if args.mark:
        print(f'"{args.mark}" at +{mark}s' if mark is not None else f'"{args.mark}" not found in the windows')
    print('window   ' + ' '.join(f'{int(i * width):>5}-{int((i + 1) * width):<5}' for i in range(args.n)))
    for key, how in ROWS:
        if any(r[key] for r in rows):
            print(f'{key:<8} ' + ' '.join(f'{fold[how](r[key]) if r[key] else "-":>11}' for r in rows))


if __name__ == '__main__':
    main()
