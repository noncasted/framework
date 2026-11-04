# The commands of profile.py over several runs: cmp (profiles side by side) and fit (a cost against a count of objects).
#
# A run is a profile with its frame count; values are inclusive ms/frame of a row. A row is a short function name
# (prefix match) or a synthetic row of Profile.run_rows: '#busy', '#(program)', '#(idle)', '#phase <Phase>'.
import collections

from cpuprofile import ProfileError, lookup

GENERIC_ROWS = ['#busy', '#(program)', '#(garbage collector)']

# Not work of the page: left out of the growth list of cmp (still rows when asked).
NOT_GROWTH = {'#(idle)', '#(program)'}


def _value(run, key, exact=False):
    frames, inc = run
    return (inc.get(key, 0) if exact else lookup(inc, key)) / 1000 / frames


def _default_rows(incs):
    """The synthetic rows present in any run: busy, program, gc, then the phases by their time in the last run."""
    present = set().union(*incs)
    phases = sorted((k for k in present if k.startswith('#phase ')), key=lambda k: -incs[-1].get(k, 0))
    return [(k, k) for k in GENERIC_ROWS + phases if k in present]


def cmp(runs, rows, growth, out):
    """runs: [(name, frames, inc)]. The rows for every run, then the growth biggest changes first->last run."""
    data = [(f, inc) for _, f, inc in runs]
    for name, f, _ in runs:
        out(f'{name}: frames {f:.0f}')
    rows = rows or _default_rows([inc for _, _, inc in runs])
    widths = [max(9, len(n) + 1) for n, _, _ in runs]
    out(f'{"":60s}' + ''.join(f'{n:>{w}s}' for (n, _, _), w in zip(runs, widths)))
    for label, key in rows:
        out(f'{label[:60]:60s}' + ''.join(f'{_value(r, key):{w}.2f}' for r, w in zip(data, widths)))
    out('')
    out('-- growth first->last (ms/f)')
    keys = (set(data[0][1]) | set(data[-1][1])) - NOT_GROWTH
    ordered = sorted(keys, key=lambda k: (-(_value(data[-1], k, True) - _value(data[0], k, True)), k))
    for k in ordered[:growth]:
        out(f'{k[:80]:80s}' + ''.join(f'{_value(r, k, True):8.2f}' for r in data))


def fit(runs, rows, bucket, ref, count, out):
    """runs: [(name, objects, frames, inc)]. Runs are grouped by the object count rounded to bucket; per row the mean
    ms/frame at each count, the least-squares slope per 100 objects and the share of '#busy' at the count ref."""
    by = collections.defaultdict(list)
    for name, n, f, inc in runs:
        by[int(round(n / float(bucket))) * bucket].append((name, (f, inc)))
    counts = sorted(by)
    if len(counts) < 2:
        raise ProfileError(f'fit needs at least two object counts, got {counts}')
    ref = counts[-1] if ref is None else ref
    if ref not in by:
        raise ProfileError(f'--ref {ref} is not one of the counts {counts}')
    if not rows:
        rows = _default_rows([inc for _, _, _, inc in runs])
        biggest = by[counts[-1]][0][1][1]
        rows += [(k, k) for k, _ in biggest.most_common() if not k.startswith('#')][:count]

    def mean(n, key):
        return sum(_value(r, key) for _, r in by[n]) / len(by[n])

    out('runs per N: ' + str({n: [name for name, _ in by[n]] for n in counts})
        + '  frames: ' + str({n: [round(r[0]) for _, r in by[n]] for n in counts}))
    out(f'{"ms/frame":42s}' + ''.join(f'{n:>8d}' for n in counts) + f'   /100b   %busy@{ref}')
    busy_ref = mean(ref, '#busy')
    xs = [n for n in counts for _ in by[n]]
    mx = sum(xs) / len(xs)
    for label, key in rows:
        means = [mean(n, key) for n in counts]
        ys = [_value(r, key) for n in counts for _, r in by[n]]
        my = sum(ys) / len(ys)
        slope = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / sum((x - mx) ** 2 for x in xs) * 100
        pct = f'{100 * mean(ref, key) / busy_ref:6.1f}' if busy_ref else ''
        out(f'{label[:42]:42s}' + ''.join(f'{m:8.2f}' for m in means) + f'{slope:8.2f}   {pct}')
