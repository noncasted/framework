# The loader of a Chrome DevTools .cpuprofile and the per-sample stacks every command of profile.py works on.
#
# A profile is a tree of nodes (callFrame.functionName, children) plus the sampled leaf node of each sample and the time
# since the previous sample (timeDeltas, microseconds). The time of a sample belongs to its leaf node; the stack of a
# sample is the chain from that leaf to the root.
import collections
import json
import os
import re

# Names of the rows of a run are cut to it.
RUN_WIDTH = 90

IDLE_ROOTS = ('(idle)', '(program)', '(garbage collector)')

# Unity player loop phases: the C++ registrators of the loop systems appear in the stack of a named (debug) wasm.
PHASE_ANY = re.compile(r'::(\w+?)Registrator::Forward')
PHASE_GROUP = re.compile(r'(EarlyUpdate|FixedUpdate|PreUpdate|PreLateUpdate|PostLateUpdate|Update|Initialization|TimeUpdate)')
PHASE_KNOWN = re.compile(
    r'::(EarlyUpdate|FixedUpdate|PreUpdate|PreLateUpdate|PostLateUpdate|Update|Initialization|TimeUpdate)\w*Registrator::Forward')

# A wasm frame without a name: the build has no names section (the release wasm, not the one of debug-wasm.sh).
UNNAMED = re.compile(r'^wasm-function\[\d+\]$')

# il2cpp appends a 40-digit hash to method names (_m<HEX>, optionally _gshared); it is noise in a report.
_HASH = re.compile(r'_m[0-9A-F]{40}(_gshared)?')


def short(name, width):
    """The il2cpp hash removed, cut to width."""
    return _HASH.sub('', name or '(anon)')[:width]


class ProfileError(Exception):
    pass


class Profile:
    def __init__(self, path):
        self.path = path
        with open(path) as f:
            data = json.load(f)
        for key in ('nodes', 'samples', 'timeDeltas'):
            if key not in data:
                raise ProfileError(f'{path}: not a .cpuprofile, no "{key}"')
        if len(data['samples']) != len(data['timeDeltas']):
            raise ProfileError(f'{path}: {len(data["samples"])} samples but {len(data["timeDeltas"])} timeDeltas')
        self.nodes = {n['id']: n for n in data['nodes']}
        self.parent = {}
        for n in data['nodes']:
            for c in n.get('children', []):
                self.parent[c] = n['id']
        # Time per sampled leaf node, microseconds.
        self.leaf_time = collections.Counter()
        for s, d in zip(data['samples'], data['timeDeltas']):
            self.leaf_time[s] += d
        self.total_us = sum(self.leaf_time.values())
        self.start_time = data.get('startTime')
        self.end_time = data.get('endTime')

    def duration_ms(self):
        if self.start_time is None or self.end_time is None:
            raise ProfileError(f'{self.path}: no startTime/endTime, give --frames instead of --frame-ms')
        return (self.end_time - self.start_time) / 1000

    def unnamed_share(self):
        """The share of the nodes that are wasm frames without a name (wasm-function[N])."""
        return sum(1 for nid in self.nodes if UNNAMED.match(self.name(nid))) / len(self.nodes)

    def name(self, nid):
        return self.nodes[nid]['callFrame']['functionName'] or '(anon)'

    def chain(self, nid):
        """Node ids from the leaf nid up to the root."""
        out = []
        while nid is not None:
            out.append(nid)
            nid = self.parent.get(nid)
        return out

    def stacks(self):
        """(time_us, [names leaf..root]) per sampled leaf node."""
        for nid, t in self.leaf_time.items():
            yield t, [self.name(x) for x in self.chain(nid)]

    def self_and_inclusive(self, key=lambda s: s):
        """Self and inclusive time (us) by key(name); a name counted once per stack in inclusive."""
        self_t = collections.Counter()
        incl = collections.Counter()
        for t, names in self.stacks():
            self_t[key(names[0])] += t
            seen = set()
            for nm in names:
                k = key(nm)
                if k not in seen:
                    incl[k] += t
                    seen.add(k)
        return self_t, incl

    def run_rows(self):
        """Inclusive time (us) by short name plus the synthetic rows of a run: '#busy', '#(idle)', '#(program)',
        '#(garbage collector)', '#phase <Phase>' ('#phase outside loop (GL js)' for busy time outside the player loop)."""
        inc = collections.Counter()
        for t, names in self.stacks():
            root = names[0]
            if root in IDLE_ROOTS:
                inc['#' + root] += t
                continue
            inc['#busy'] += t
            phase = None
            for nm in reversed(names):
                m = PHASE_KNOWN.search(nm)
                if m:
                    phase = m.group(1)
                    break
            inc['#phase ' + (phase or 'outside loop (GL js)')] += t
            seen = set()
            for nm in names:
                s = short(nm, RUN_WIDTH)
                if s not in seen:
                    inc[s] += t
                    seen.add(s)
        return inc


def frames_of(profile, frames, frame_ms):
    """The frame count of a profile: given, its duration over the mean frame time, or, with neither, the frames of the
    prof step of the stand that wrote it (summary.json next to the profile, the step whose file is the profile)."""
    if frames is not None and frame_ms is not None:
        raise ProfileError('give one of --frames and --frame-ms, not both')
    if frames is not None:
        if frames <= 0:
            raise ProfileError(f'--frames must be positive, got {frames}')
        return frames
    if frame_ms is not None:
        if frame_ms <= 0:
            raise ProfileError(f'--frame-ms must be positive, got {frame_ms}')
        return profile.duration_ms() / frame_ms
    return _stand_frames(profile.path)


def _stand_frames(path):
    summary = os.path.join(os.path.dirname(os.path.abspath(path)), 'summary.json')
    if not os.path.isfile(summary):
        raise ProfileError(f'{path}: give --frames or --frame-ms (no summary.json of the stand next to it)')
    with open(summary) as f:
        steps = json.load(f).get('steps', [])
    base = os.path.basename(path)
    found = [s for s in steps if s.get('kind') == 'prof' and s.get('file') == base]
    if not found or not found[0].get('frames'):
        raise ProfileError(f'{path}: give --frames or --frame-ms ({summary} has no prof step of it with frames)')
    return found[0]['frames']


def read_rows(path):
    """Rows of cmp / fit from a file: one per line, 'key' or 'label<TAB>key'; '#' comments are lines starting '# '."""
    rows = []
    with open(path) as f:
        for line in f:
            line = line.rstrip('\n')
            if not line.strip() or line.startswith('# '):
                continue
            label, _, key = line.partition('\t')
            rows.append((label, key or label))
    if not rows:
        raise ProfileError(f'{path}: no rows')
    return rows


def lookup(inc, key):
    """The value of a row: the exact key, else the first key starting with it (long C++ signatures are cut)."""
    if not key.startswith('#'):
        key = short(key, RUN_WIDTH)
    if key in inc:
        return inc[key]
    if not key.startswith('#'):
        for k in inc:
            if k.startswith(key):
                return inc[k]
    return 0
