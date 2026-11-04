# The phases command of profile.py: time per Unity player-loop phase and the top inclusive functions inside each phase.
import collections
import re

from cpuprofile import IDLE_ROOTS, PHASE_ANY, PHASE_GROUP, short


def phases(profile, frames, count, phase_filter, out):
    """ms/frame per phase (grouped by the leading player-loop word), then for the 6 biggest phases their registrators
    ('sub') and the count functions with the most inclusive time inside them."""
    per_phase = collections.Counter()
    inc = collections.defaultdict(collections.Counter)
    total = 0
    busy = 0
    for t, chain in profile.stacks():
        total += t
        if chain[0] not in IDLE_ROOTS:
            busy += t
        ph = None
        for nm in reversed(chain):
            m = PHASE_ANY.search(nm)
            if m:
                ph = m.group(1)
                break
        if ph is None:
            ph = 'other:' + chain[0] if chain[0] in IDLE_ROOTS else 'other'
        g = PHASE_GROUP.match(ph)
        gp = g.group(1) if g else ph
        per_phase[gp] += t
        seen = set()
        for nm in chain:
            s = short(nm, 110)
            if s not in seen:
                inc[gp][s] += t
                seen.add(s)
        inc[gp + '#sub'][short(ph, 110)] += t

    def f(t):
        return f'{t / 1000 / frames:7.2f}'

    out(f'total {total / 1000:.0f} ms, busy {busy / 1000:.0f} ms, frames {frames:.0f}, busy/frame {busy / 1000 / frames:.2f}')
    for gp, t in per_phase.most_common():
        out(f'{f(t)} ms/f  {100 * t / busy:5.1f}%  {gp}')
    for gp, t in per_phase.most_common(6):
        if gp.startswith('other') or (phase_filter and not re.search(phase_filter, gp)):
            continue
        out('')
        out(f'== {gp} {f(t)}')
        for k, v in inc[gp + '#sub'].most_common(8):
            out(f'  sub {f(v)} {k}')
        for k, v in inc[gp].most_common(count):
            out(f'    {f(v)} {k}')
