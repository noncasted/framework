#!/usr/bin/env python3
"""Overridden Unity packages: embedded copies in client/Packages that carry local patches.

packages.json (.claude/override-packages of the project) lists them: where the pristine package comes from, the version / commit the
embedded copy is based on, and what is left out of the copy. patches/<name>.patch is the local change on top of the
pristine base.

  status                 embedded vs upstream version, state of the local patch
  diff <name>|--all      rewrite patches/<name>.patch from the pristine base and the embedded copy
  update <name>|--all    take the upstream version, re-apply the patch, bump packages.json and manifest.json
                         (--force: also when the version did not change - re-embeds the same version)

Exit code 2: the patch did not apply cleanly, *.rej files are left in the package for a manual merge.
"""
import json, os, re, shutil, subprocess, sys, tarfile, tempfile, urllib.request

REPO = subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], text=True).strip()
DATA = os.path.join(REPO, '.claude', 'override-packages')
CLIENT = os.path.join(REPO, 'client')
PACKAGES = os.path.join(CLIENT, 'Packages')
REGISTRY = os.path.join(DATA, 'packages.json')
PATCHES = os.path.join(DATA, 'patches')
BUILTIN = 'Editor/Data/Resources/PackageManager/BuiltInPackages'


def run(*args, cwd=None, check=True):
    result = subprocess.run(args, cwd=cwd, capture_output=True, text=True)
    if check and result.returncode != 0:
        raise SystemExit(f'{" ".join(args)}\n{result.stdout}{result.stderr}')
    return result


def editor_roots():
    """Installed editors, the one of the project first."""
    with open(os.path.join(CLIENT, 'ProjectSettings', 'ProjectVersion.txt')) as f:
        version = re.search(r'm_EditorVersion: (\S+)', f.read()).group(1)
    bases = [os.environ.get('UNITY_EDITORS'), '/tools/unity', os.path.expanduser('~/Unity/Hub/Editor')]
    roots = []
    for base in filter(None, bases):
        if os.path.isdir(base):
            names = sorted(os.listdir(base), key=lambda n: n != version)
            roots += [os.path.join(base, n) for n in names if os.path.isdir(os.path.join(base, n, BUILTIN))]
    if not roots or os.path.basename(roots[0]) != version:
        raise SystemExit(f'Editor {version} not found (set UNITY_EDITORS to the folder with the installed editors)')
    return roots


def package_version(folder):
    with open(os.path.join(folder, 'package.json')) as f:
        return json.load(f)['version']


def git_commit(source):
    """Commit of a git source: ref is a branch (its head) or refs/tags/<tag> (a package pinned on a tag)."""
    ref = source['ref']
    if ref.startswith('refs/tags/') == False:
        ref = f'refs/heads/{ref}'
    # The object of an annotated tag is not a commit: its commit is on the line of the peeled name.
    lines = run('git', 'ls-remote', source['url'], ref, ref + '^{}').stdout.splitlines()
    if not lines:
        raise SystemExit(f'{source["url"]}: {ref} not found')
    return lines[-1].split()[0]


def latest(package):
    """Upstream version (builtin, registry) or commit (git) the package should be on."""
    source = package['source']
    if source['type'] == 'builtin':
        # The editor is part of the identity: editors ship different contents under the same package version.
        root = editor_roots()[0]
        return f'{package_version(os.path.join(root, BUILTIN, package["name"]))}@{os.path.basename(root)}'
    if source['type'] == 'registry':
        with urllib.request.urlopen(f'{source["url"]}/{package["name"]}') as response:
            return json.load(response)['dist-tags']['latest']
    if source['type'] == 'git':
        return git_commit(source)
    if source['type'] == 'repo':
        # No fetchable upstream: a new version is imported by hand, the base stays until then.
        return package['base']
    raise SystemExit(f'Unknown source type {source["type"]}')


def fetch(package, base, target):
    """Pristine package of the given version / commit into target, without the excluded entries."""
    source = package['source']
    name = package['name']
    if source['type'] == 'builtin':
        version, editor = base.split('@')
        folders = [os.path.join(root, BUILTIN, name) for root in editor_roots() if os.path.basename(root) == editor]
        folders = [f for f in folders if os.path.isdir(f) and package_version(f) == version]
        if not folders:
            return False
        shutil.copytree(folders[0], target)
    elif source['type'] == 'registry':
        with tempfile.TemporaryDirectory() as tmp:
            archive = os.path.join(tmp, 'package.tgz')
            urllib.request.urlretrieve(f'{source["url"]}/{name}/-/{name}-{base}.tgz', archive)
            with tarfile.open(archive) as tar:
                tar.extractall(tmp, filter='data')
            shutil.move(os.path.join(tmp, 'package'), target)
    elif source['type'] == 'repo':
        # The pristine copy is the package folder at the base commit of this repository.
        folder = f'client/Packages/{name}'
        with tempfile.TemporaryDirectory() as tmp:
            # checkout (not archive) runs the LFS smudge filter - with the repo .gitattributes, an outside work
            # tree has none of its own; a temporary index leaves the repo index alone.
            tree = os.path.join(tmp, 'tree')
            os.makedirs(tree)
            env = dict(os.environ, GIT_INDEX_FILE=os.path.join(tmp, 'index'))
            attributes = os.path.join(REPO, '.gitattributes')
            result = subprocess.run(['git', '-c', f'core.attributesFile={attributes}', f'--work-tree={tree}',
                                     'checkout', base, '--', folder], cwd=REPO,
                                    env=env, capture_output=True, text=True)
            if result.returncode != 0:
                raise SystemExit(f'{name}: git checkout {base}\n{result.stdout}{result.stderr}')
            shutil.move(os.path.join(tree, folder), target)
    elif source['type'] == 'git':
        with tempfile.TemporaryDirectory() as tmp:
            run('git', 'init', '-q', cwd=tmp)
            run('git', 'fetch', '-q', '--depth', '1', source['url'], base, cwd=tmp)
            run('git', 'checkout', '-q', 'FETCH_HEAD', cwd=tmp)
            shutil.move(os.path.join(tmp, source.get('path', '')), target)
    else:
        raise SystemExit(f'Unknown source type {source["type"]}')
    for entry in package.get('exclude', []):
        path = os.path.join(target, entry)
        if os.path.isdir(path):
            shutil.rmtree(path)
        elif os.path.exists(path):
            os.remove(path)
    # Folders our change moves (binary files a patch cannot carry): moved in the pristine copy before the diff
    # and before the patch is applied.
    for source_path, target_path in package.get('moves', []):
        destination = os.path.join(target, target_path)
        os.makedirs(os.path.dirname(destination), exist_ok=True)
        shutil.move(os.path.join(target, source_path), destination)
        if os.path.exists(os.path.join(target, source_path + '.meta')):
            shutil.move(os.path.join(target, source_path + '.meta'), destination + '.meta')
    return True


def make_patch(package, pristine):
    """Unified diff pristine -> embedded, paths a/... b/...; '' when there is no local change."""
    with tempfile.TemporaryDirectory() as tmp:
        os.symlink(pristine, os.path.join(tmp, 'a'))
        os.symlink(os.path.join(PACKAGES, package['name']), os.path.join(tmp, 'b'))
        result = run('diff', '-ruN', 'a', 'b', cwd=tmp, check=False)
    if result.returncode > 1 or 'Binary files' in result.stdout:
        raise SystemExit(f'{package["name"]}: cannot diff\n{result.stdout[-2000:]}{result.stderr}')
    # Drop the timestamps of the file headers: the patch must not change from run to run.
    return re.sub(r'^(---|\+\+\+) (\S+)\t.*$', r'\1 \2', result.stdout, flags=re.M)


def patch_path(package):
    return os.path.join(PATCHES, package['name'] + '.patch')


def read_patch(package):
    path = patch_path(package)
    return open(path).read() if os.path.exists(path) else ''


def refresh_patch(package, write):
    """Compares the stored patch with the embedded copy. Returns 'ok', 'changed' or 'no base'."""
    with tempfile.TemporaryDirectory() as tmp:
        pristine = os.path.join(tmp, 'pristine')
        if fetch(package, package['base'], pristine) == False:
            return 'no base'
        patch = make_patch(package, pristine)
    if patch == read_patch(package):
        return 'ok'
    if write:
        os.makedirs(PATCHES, exist_ok=True)
        with open(patch_path(package), 'w') as f:
            f.write(patch)
    return 'changed'


def short(package, value):
    return value[:12] if package['source']['type'] in ('git', 'repo') else value


def status(packages):
    for package in packages:
        target = latest(package)
        state = refresh_patch(package, write=False)
        lines = sum(1 for l in read_patch(package).splitlines() if re.match(r'^[+-](?![+-])', l))
        update = 'up to date' if target == package['base'] else f'UPDATE -> {short(package, target)}'
        note = {'ok': '', 'changed': ', embedded copy differs from the stored patch (run diff)',
                'no base': ', base version is not installed - patch not verified'}[state]
        print(f'{package["name"]}: {package["source"]["type"]} {short(package, package["base"])}, {update}, '
              f'patch {lines} lines{note}')


def update(package, registry, force):
    name = package['name']
    target = latest(package)
    if target == package['base'] and force == False:
        print(f'{name}: up to date ({short(package, target)})')
        return 0

    # Local edits made since the last diff go into the patch first.
    if refresh_patch(package, write=True) == 'no base':
        print(f'{name}: base {package["base"]} is not installed, using the stored patch as is')

    embedded = os.path.join(PACKAGES, name)
    with tempfile.TemporaryDirectory() as tmp:
        pristine = os.path.join(tmp, 'pristine')
        if fetch(package, target, pristine) == False:
            raise SystemExit(f'{name}: {target} not found')
        shutil.rmtree(embedded)
        shutil.copytree(pristine, embedded)

    rejected = 0
    if read_patch(package).strip():
        result = run('patch', '-p1', '--no-backup-if-mismatch', '-i', patch_path(package), cwd=embedded, check=False)
        rejected = result.returncode
        if rejected:
            print(f'{name}: PATCH DID NOT APPLY CLEANLY\n{result.stdout}{result.stderr}')

    old = package['base']
    package['base'] = target
    with open(REGISTRY, 'w') as f:
        json.dump(registry, f, indent=2)
        f.write('\n')

    # A plain version in manifest.json follows the package, a git url stays pinned on the base commit;
    # file: paths stay.
    manifest_path = os.path.join(PACKAGES, 'manifest.json')
    manifest = open(manifest_path).read()
    version = package_version(embedded)
    bumped = re.sub(rf'("{re.escape(name)}": ")\d[^"]*(")', rf'\g<1>{version}\g<2>', manifest)
    if package['source']['type'] == 'git':
        bumped = re.sub(rf'("{re.escape(name)}": "[^"#]*#)[^"]*(")', rf'\g<1>{target}\g<2>', bumped)
    if bumped != manifest:
        open(manifest_path, 'w').write(bumped)

    print(f'{name}: {short(package, old)} -> {short(package, target)} (package.json {version})'
          + (', resolve the *.rej files, then run diff' if rejected else ', patch applied'))
    return 2 if rejected else 0


def main(args):
    with open(REGISTRY) as f:
        registry = json.load(f)
    command = args[0] if args else 'status'
    names = [a for a in args[1:] if not a.startswith('--')]
    packages = [p for p in registry['packages'] if '--all' in args or p['name'] in names]
    missing = set(names) - {p['name'] for p in packages}
    if missing:
        raise SystemExit(f'Not in packages.json: {", ".join(sorted(missing))}')

    if command == 'status':
        status(packages or registry['packages'])
        return 0
    if not packages:
        raise SystemExit(__doc__)
    if command == 'diff':
        for package in packages:
            print(f'{package["name"]}: patch {refresh_patch(package, write=True)}')
        return 0
    if command == 'update':
        return max(update(package, registry, '--force' in args) for package in packages)
    raise SystemExit(__doc__)


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
