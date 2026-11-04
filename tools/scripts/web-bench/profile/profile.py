#!/usr/bin/env python3
# Reports over Chrome DevTools CPU profiles (.cpuprofile) of a WebGL build, best with the debug wasm (debug-wasm.sh) so
# the wasm frames carry function names. Python 3 stdlib only.
#
#   profile.py top <cpuprofile> [regex] [N]             self top N (25), then top N inclusive of the names matching regex; ms
#   profile.py callers <cpuprofile> <prefix> [depth]    call paths of the outermost frame starting with prefix; ms
#   profile.py kids <cpuprofile> <regex> --frames F     children of the frames matching regex; ms/frame
#   profile.py selfunder <cpuprofile> <regex> --frames F  self time of the functions under regex; ms/frame
#   profile.py phases <cpuprofile> --frames F           time per Unity player-loop phase; ms/frame
#   profile.py cmp <a> <b> [...] --frames Fa,Fb         rows side by side and the biggest growth a -> last; ms/frame
#   profile.py fit --run <objects> <frames> <cpuprofile> ...   mean per object count and slope per 100 objects
#
# Frames: --frames F, or --frame-ms M (frames = the profile duration / M), or neither: the frames the prof step of the stand
# counted (summary.json next to the profile). A profile without wasm names (wasm-function[N]) gets a warning: phases and
# rows need the debug wasm (debug-wasm.sh). The growth list of cmp leaves out (idle) and (program). cmp and fit take --rows <file>: one row per
# line, 'key' or 'label<TAB>key', a key is a function name (prefix match) or '#busy', '#(program)', '#phase Update', ...
#
# Example:
#   profile.py top run/cpu.cpuprofile 'Render|Cull' 30
#   profile.py fit --run 50 300 a50/cpu.cpuprofile --run 300 250 a300/cpu.cpuprofile --rows rows.tsv
import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import phases as phases_cmd  # noqa: E402
import runs  # noqa: E402
import tree  # noqa: E402
from cpuprofile import Profile, ProfileError, frames_of, read_rows  # noqa: E402


# A profile whose nodes are mostly wasm-function[N] has no names: above this share the commands warn.
UNNAMED_WARN = 0.5


def _load(path, warn):
    prof = Profile(path)
    share = prof.unnamed_share()
    if share > UNNAMED_WARN:
        warn(f'profile.py: warning: {path}: {100 * share:.0f}% of the nodes are wasm-function[N] - no function names, '
             f'phases and rows are not found; profile the build with its debug wasm (debug-wasm.sh)')
    return prof


def _warn(text):
    print(text, file=sys.stderr)


def _regex(text):
    try:
        return re.compile(text)
    except re.error as e:
        raise ProfileError(f'bad regex {text!r}: {e}')


def _frames_args(p, many=False):
    kind = str if many else float
    p.add_argument('--frames', type=kind, help='frame count of the profile' + (' (comma list, one per profile)' if many else '')
                   + '; default: the frames of the prof step in summary.json next to the profile')
    p.add_argument('--frame-ms', type=kind, help='mean frame time, ms; frames = duration / it' + (' (comma list)' if many else ''))


def _floats(text, n, flag):
    if text is None:
        return [None] * n
    values = [float(x) for x in text.split(',')]
    if len(values) == 1:
        values *= n
    if len(values) != n:
        raise ProfileError(f'{flag}: {len(values)} values for {n} profiles')
    return values


def _run_names(paths):
    """Short names of the runs: the file name, with its folder when the file names repeat."""
    bases = [os.path.splitext(os.path.basename(p))[0] for p in paths]
    if len(set(bases)) == len(bases):
        return bases
    return [os.path.basename(os.path.dirname(os.path.abspath(p))) + '/' + b for p, b in zip(paths, bases)]


def _header():
    """The comment at the top of this file, the text of --help."""
    lines = []
    with open(__file__) as f:
        for line in f.read().splitlines()[1:]:
            if not line.startswith('#'):
                break
            lines.append(line[2:])
    return '\n'.join(lines)


def parse(argv):
    ap = argparse.ArgumentParser(prog='profile.py', description=_header(),
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='command', required=True)

    p = sub.add_parser('top', help='self top and inclusive time of matching names (was an.py)')
    p.add_argument('profile')
    p.add_argument('regex', nargs='?')
    p.add_argument('count', nargs='?', type=int, default=25, help='self top size (25)')

    p = sub.add_parser('callers', help='call paths of a function (was callers.py)')
    p.add_argument('profile')
    p.add_argument('prefix', help='function name prefix')
    p.add_argument('depth', nargs='?', type=int, default=6, help='frames per path (6)')
    p.add_argument('-n', '--count', type=int, default=12, help='paths shown (12)')

    p = sub.add_parser('kids', help='children of a function, wrappers skipped (was kids.py)')
    p.add_argument('profile')
    p.add_argument('regex')
    _frames_args(p)
    p.add_argument('--depth', type=int, default=1, help='levels of children joined with > (1)')
    p.add_argument('-n', '--count', type=int, default=30)

    p = sub.add_parser('selfunder', help='self time inside the subtree of a function (was selfunder.py)')
    p.add_argument('profile')
    p.add_argument('regex')
    _frames_args(p)
    p.add_argument('-n', '--count', type=int, default=25)

    p = sub.add_parser('phases', help='time per player-loop phase (was ph.py)')
    p.add_argument('profile')
    _frames_args(p)
    p.add_argument('-n', '--count', type=int, default=25, help='functions per phase (25)')
    p.add_argument('--filter', help='regex: detail only the phases matching it')

    p = sub.add_parser('cmp', help='profiles side by side (was cmp.py)')
    p.add_argument('profiles', nargs='+')
    _frames_args(p, many=True)
    p.add_argument('--rows', help='rows file (default: busy, program, gc, phases)')
    p.add_argument('--growth', type=int, default=45, help='rows of the growth list (45)')

    p = sub.add_parser('fit', help='mean per object count and slope per 100 objects (was fit.py)')
    p.add_argument('--run', nargs=3, action='append', required=True, metavar=('OBJECTS', 'FRAMES', 'PROFILE'),
                   help='a run: the object count (bodies), its frame count, its profile; repeat per run')
    p.add_argument('--rows', help='rows file (default: busy, program, gc, phases, top inclusive of the biggest run)')
    p.add_argument('--bucket', type=int, default=50, help='object counts rounded to it (50)')
    p.add_argument('--ref', type=int, help='object count of the %%busy column (default: the biggest)')
    p.add_argument('-n', '--count', type=int, default=25, help='top functions when no --rows (25)')

    args = ap.parse_args(argv)
    if args.command == 'cmp' and len(args.profiles) < 2:
        ap.error('cmp needs at least two profiles')
    return args


def main(argv, out=print, warn=_warn):
    args = parse(argv)
    c = args.command
    if c == 'top':
        tree.top(_load(args.profile, warn), _regex(args.regex) if args.regex else None, args.count, out)
    elif c == 'callers':
        tree.callers(_load(args.profile, warn), args.prefix, args.depth, args.count, out)
    elif c in ('kids', 'selfunder', 'phases'):
        prof = _load(args.profile, warn)
        frames = frames_of(prof, args.frames, args.frame_ms)
        if c == 'kids':
            tree.kids(prof, _regex(args.regex), args.regex, frames, args.depth, args.count, out)
        elif c == 'selfunder':
            tree.selfunder(prof, _regex(args.regex), args.regex, frames, args.count, out)
        else:
            phases_cmd.phases(prof, frames, args.count, args.filter, out)
    elif c == 'cmp':
        n = len(args.profiles)
        frames = _floats(args.frames, n, '--frames')
        frame_ms = _floats(args.frame_ms, n, '--frame-ms')
        data = []
        for path, name, f, fm in zip(args.profiles, _run_names(args.profiles), frames, frame_ms):
            prof = _load(path, warn)
            data.append((name, frames_of(prof, f, fm), prof.run_rows()))
        runs.cmp(data, read_rows(args.rows) if args.rows else None, args.growth, out)
    elif c == 'fit':
        paths = [r[2] for r in args.run]
        data = []
        for (objects, frames, path), name in zip(args.run, _run_names(paths)):
            data.append((name, int(objects), frames_of(None, float(frames), None), _load(path, warn).run_rows()))
        runs.fit(data, read_rows(args.rows) if args.rows else None, args.bucket, args.ref, args.count, out)


if __name__ == '__main__':
    try:
        main(sys.argv[1:])
    except (ProfileError, OSError, ValueError) as e:
        print(f'profile.py: {e}', file=sys.stderr)
        sys.exit(1)
