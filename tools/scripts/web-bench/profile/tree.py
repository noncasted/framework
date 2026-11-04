# The commands of profile.py over the call tree of one profile: top, callers, kids, selfunder.
import collections
import re

from cpuprofile import ProfileError, short

# Wrappers between a function and its real children in an il2cpp / emscripten stack.
WRAPPERS = re.compile(r'^(wasm-function|il2cpp::|RuntimeInvoker|ScriptingInvocation|UpdatePass_Run|Action_Invoke|'
                      r'UpdateFunction_Invoke|InterfaceFuncInvoker|VirtualActionInvoker|GenericInterfaceAction|(anon)$)')


def top(profile, pattern, count, out):
    """Total time, the count functions with the most self time, then the count biggest inclusive times of the names
    matching pattern; ms."""
    self_t, incl = profile.self_and_inclusive()
    out(f'total ms {round(profile.total_us / 1000)}')
    out('--self top')
    for k, v in self_t.most_common(count):
        out(f'{v / 1000:8.1f} {k[:120]}')
    if pattern is not None:
        out('--incl matching')
        for k, v in [kv for kv in incl.most_common() if pattern.search(kv[0])][:count]:
            out(f'{v / 1000:8.1f} {k[:140]}')


def callers(profile, prefix, depth, count, out):
    """The call paths (up to depth frames, from the outermost frame starting with prefix towards the root) by time, ms."""
    paths = collections.Counter()
    for nid, t in profile.leaf_time.items():
        chain = [profile.name(x)[:70] for x in profile.chain(nid)]
        idx = [i for i, c in enumerate(chain) if c.startswith(prefix)]
        if not idx:
            continue
        i = idx[-1]
        paths[' <- '.join(chain[i:i + depth])] += t
    for k, v in paths.most_common(count):
        out(f'{v / 1000:7.1f} {k}')


def kids(profile, pattern, label, frames, depth, count, out):
    """Inclusive ms/frame of the descendants at depth under the outermost frame matching pattern, wrappers skipped."""
    found = collections.Counter()
    total = 0
    for t, names in profile.stacks():
        chain = [short(nm, 100) for nm in reversed(names)]
        idx = [i for i, c in enumerate(chain) if pattern.search(c)]
        if not idx:
            continue
        i = idx[0]
        total += t
        rest = [c for c in chain[i + 1:] if not WRAPPERS.search(c)]
        found[' > '.join(rest[:depth]) if rest else '(self)'] += t
    if not total:
        raise ProfileError(f'no sample has a frame matching {label}')
    out(f'{total / 1000 / frames:.2f} ms/f under {label}')
    for k, v in found.most_common(count):
        out(f'{v / 1000 / frames:7.2f} {100 * v / total:5.1f}% {k}')


def selfunder(profile, pattern, label, frames, count, out):
    """Self time ms/frame of the functions inside the subtree of any frame matching pattern."""
    found = collections.Counter()
    total = 0
    for t, names in profile.stacks():
        if not any(pattern.search(short(nm, 110)) for nm in names):
            continue
        found[short(names[0], 110)] += t
        total += t
    if not total:
        raise ProfileError(f'no sample has a frame matching {label}')
    out(f'{total / 1000 / frames:.2f} ms/f in {label}')
    for k, v in found.most_common(count):
        out(f'{v / 1000 / frames:6.2f} {100 * v / total:5.1f}% {k}')
