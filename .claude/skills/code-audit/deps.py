# Dependency direction check (P14). stdin: the .cs files of the audited area. argv[1]: .claude/code-audit.json
# of the project: root (the code of the game), utility and feature (substrings of the path).
# A "feature" is the folder of a file. Reported:
#   sibling  - a concrete type of a sibling feature folder (same parent) is named (interfaces are counted, not listed)
#   composer - the scope extension / composer of its own or a parent folder is named by a feature class
#   upward   - a utility file names a type of a game feature
import collections, json, os, re, sys

CONFIG = json.load(open(sys.argv[1], encoding='utf-8'))
ROOT = CONFIG['root']
TOP = re.compile(r'^    (?:\[[^\]]*\] *)?(?:public |internal |private )?(?:sealed |static |abstract |readonly |partial |ref |unsafe )*'
                 r'(?:class|struct|interface|enum|record) +(\w+)', re.M)
WORD = re.compile(r'\b[A-Z]\w+\b')
COMMENT = re.compile(r'//.*?$|/\*.*?\*/', re.M | re.S)
UTIL = tuple(CONFIG['utility'])
FEATURE = tuple(CONFIG['feature'])
SHARED_DIRS = ('/Definitions', '/Pools', '/Hits', '/Particles', '/Common')  # data and utilities: depending on them is downward
DATA = re.compile(r'(Options|Definition|Kind|Visuals|Looks?|Texts|Catalog|Id|Info|Team|Stat|Ecs)$')

home = {}
for d, _, files in os.walk(ROOT):
    if '/Generated' in d or '/Editor' in d or '/Tools' in d or '/Tests' in d:
        continue
    for n in files:
        if n.endswith('.cs') and not n.endswith('.g.cs'):
            path = os.path.join(d, n)
            for t in TOP.findall(open(path, encoding='utf-8', errors='replace').read()):
                home.setdefault(t, path)

def composer(t):
    return t.endswith('ScopeExtensions') or (t.endswith('Extensions') and not t.endswith('ViewExtensions'))

for f in (l.strip() for l in sys.stdin if l.strip()):
    if '/Editor/' in f or '/Tools/' in f or '/Tests/' in f:
        continue
    text = COMMENT.sub('', open(f, encoding='utf-8', errors='replace').read())
    own = set(TOP.findall(text))
    if any(composer(t) for t in own):
        continue  # the composer itself may know everything below it
    fdir = os.path.dirname(f)
    out = collections.defaultdict(list)
    ifaces = 0
    for t in sorted(set(WORD.findall(text)) - own):
        h = home.get(t)
        if h is None or h == f:
            continue
        hdir = os.path.dirname(h)
        if DATA.search(t) or any(hdir.endswith(x) or (x + '/') in hdir for x in SHARED_DIRS):
            continue
        if any(u in f for u in UTIL) and any(x in h for x in FEATURE):
            out['upward'].append(t)
        elif os.path.dirname(hdir) == os.path.dirname(fdir) and hdir != fdir and not fdir.startswith(hdir):
            if re.match(r'I[A-Z]', t):
                ifaces += 1
            else:
                out['sibling'].append(f'{t}@{os.path.basename(hdir)}')
        elif composer(t) and (fdir == hdir or fdir.startswith(hdir + '/')):
            out['composer'].append(t)
    for k, v in out.items():
        print(f'{k}: {f}: {" ".join(v)}')
    if ifaces > 4:
        print(f'sibling-interfaces: {f}: {ifaces} interfaces of sibling features (P14: through the composer?)')
