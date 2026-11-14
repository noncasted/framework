#!/usr/bin/env python3
"""Measures the size of the WebGL build of one Unity version with the stripping variants of variants/.

For each variant it assembles the project projects/<version>-<variant> (template/ + the parts of the variant, the
manifest with the framework packages, the stripping list checked against the submodules of that editor), builds it
in batchmode (Measurement.MeasurementBuild.Run), measures the files of Build/ (brotli and raw, the sections of the
wasm, the wasm before the submodule stripping, the managed assemblies after the IL2CPP linker), runs the build in
headless Chromium (tools/smoke.mjs) and writes results/<version>/<variant>.json. Then RESULTS.md is rebuilt
(tools/report.py).

Usage: tools/measure.py <unity version> [variant ...] [--skip-build] [--skip-smoke] [--trace-missing] [--profile]
  <unity version>  e.g. 7000.0.0a16: the editor /tools/unity/<version>/Editor/Unity ($UNITY_EDITORS overrides /tools/unity)
  variant          names of variants/*.json, all by default
  --skip-build     measure the existing build of the project again (after a change of this script)
  --skip-smoke     no headless run (the record says so)
  --trace-missing  log every call of a stripped submodule in the browser console (Ignore otherwise, as pixel-swarm):
                   to find what a broken variant calls; the logging adds code, so the sizes of such a run are marked
  --profile        no stripping: the build is instrumented by the submodule profiling of the stripping tool, the smoke
                   run takes its report (the submodules the scene called) and profiles/<version>/<variant>.json lists
                   the submodules the scene never called that the lists of the variant do not strip yet: the
                   candidates for aggressive.txt. Writes no result.
Environment: UNITY_BUILD_SLOTS (2), the batchmode builds at once on the machine, shared with deploy-client.
"""
import argparse
import fcntl
import glob
import json
import os
import shutil
import struct
import re
import subprocess
import tarfile
import sys
import time
from datetime import datetime, timezone

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMEWORK = os.path.dirname(os.path.dirname(ROOT))
TOOLS = os.path.join(ROOT, 'tools')
SLOT_DIR = '/tmp/claude-1000'
BUILD_TIMEOUT = 3600
FRAMEWORK_PREFIX = 'framework:'
# The version of the package the editor ships (BuiltInPackages): one override for every editor of a major version.
BUILTIN = 'builtin'
OPTIONAL_PREFIX = '?'
PROFILE = 'Assets/Measurement/Profile/Web.asset'


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('version')
    parser.add_argument('variants', nargs='*')
    parser.add_argument('--skip-build', action='store_true')
    parser.add_argument('--skip-smoke', action='store_true')
    parser.add_argument('--trace-missing', action='store_true')
    parser.add_argument('--profile', action='store_true')
    args = parser.parse_args()

    editor = editor_path(args.version)
    variants = args.variants or sorted(os.path.splitext(name)[0] for name in os.listdir(os.path.join(ROOT, 'variants')))
    failed = []

    for variant in variants:
        try:
            if args.profile:
                profile(editor, args.version, variant)
            else:
                measure(editor, args.version, variant, args.skip_build, args.skip_smoke, args.trace_missing)
        except MeasurementError as error:
            print(f'FAIL {args.version} {variant}: {error}', flush=True)
            failed.append(variant)

    subprocess.run([sys.executable, os.path.join(TOOLS, 'report.py')], check=True)

    if failed:
        sys.exit(f'failed: {", ".join(failed)}')


class MeasurementError(Exception):
    pass


def applies(override, version):
    if 'unity' in override:
        return version.startswith(override['unity'])
    return version_key(version) >= version_key(override['from'])


def version_key(version):
    """7000.0.0a16 -> (7000, 0, 0, 0, 16): alpha < beta < final < patch."""
    match = re.fullmatch(r'(\d+)\.(\d+)\.(\d+)([abfp])(\d+)', version)
    if not match:
        raise MeasurementError(f'not a Unity version: {version}')
    major, minor, patch, stage, number = match.groups()
    return int(major), int(minor), int(patch), 'abfp'.index(stage), int(number)


def editor_path(version):
    path = os.path.join(os.environ.get('UNITY_EDITORS', '/tools/unity'), version, 'Editor', 'Unity')
    if not os.path.isfile(path):
        sys.exit(f'No Unity editor at {path}')
    return path


class Paths:
    def __init__(self, version, variant):
        self.name = f'{version}-{variant}'
        self.project = os.path.join(ROOT, 'projects', self.name)
        self.output = os.path.join(self.project, 'Build-WebGL')
        self.log = os.path.join(self.project, 'build.log')
        self.record = os.path.join(self.project, 'Measurement', 'record.json')


def prepare(editor, version, variant):
    config = with_overrides(read_json(os.path.join(ROOT, 'variants', f'{variant}.json')), version)
    strip, skipped = resolve_stripping(editor, config['strip'])
    strip = [name for name in strip if name not in config['keep']]
    return config, strip, skipped


def assemble_and_build(editor, version, paths, config, strip, flags):
    manifest = assemble(editor, paths.project, config, strip, version)
    for entry in config['unitypackages']:
        extract_unitypackage(editor, entry, paths.project)
    bootstrap(editor, paths.project, manifest)
    build(editor, paths, flags)
    if read_json(os.path.join(paths.project, 'Packages', 'manifest.json')) != manifest:
        raise MeasurementError('Unity changed the manifest (packages of a new project?): the build has packages the variant does not list')


def measure(editor, version, variant, skip_build, skip_smoke, trace_missing):
    config, strip, skipped = prepare(editor, version, variant)
    paths = Paths(version, variant)
    name, project, output, log, record_path = paths.name, paths.project, paths.output, paths.log, paths.record

    if skip_build:
        if not os.path.isfile(record_path):
            raise MeasurementError(f'no build of {name} to measure again')
    else:
        assemble_and_build(editor, version, paths, config, strip, ['-measurementMissing', 'LogError' if trace_missing else 'Ignore'])

    record = read_json(record_path)
    if record.get('profiled'):
        raise MeasurementError(f'the last build of {name} is instrumented for profiling (--profile): build it again')

    result = {
        'unity': version,
        'variant': variant,
        'description': config['description'],
        'measured': datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC'),
        'build': record,
        'strippingSkipped': skipped,
        'overrides': config['applied'],
        'files': measure_files(os.path.join(output, 'Build')) + streaming_files(os.path.join(output, 'StreamingAssets')),
        'wasmBeforeStripping': wasm_before_stripping(project),
        'managed': managed_assemblies(project),
        'smoke': None if skip_smoke else smoke(output, project),
    }

    if result['build'].get('missingSubmodules') != 'Ignore':
        # Not a measurement: the logging of stripped calls adds code. Only what the page did is shown.
        print(json.dumps(result['smoke'], indent=2), flush=True)
        return

    folder = os.path.join(ROOT, 'results', version)
    os.makedirs(folder, exist_ok=True)
    with open(os.path.join(folder, f'{variant}.json'), 'w') as file:
        json.dump(result, file, indent=2)
        file.write('\n')

    total = sum(f['compressed'] for f in result['files'])
    print(f'OK {name}: {total / 1024:.0f} KB compressed, smoke {result["smoke"] and result["smoke"]["status"]}', flush=True)

    if result['smoke'] and result['smoke']['status'] != 'ready':
        run = result['smoke']
        tail = '\n  '.join(run.get('console', [])[-8:])
        raise MeasurementError(f'smoke {run["status"]}: {run.get("line") or run.get("errors")}\n  last console lines:\n  {tail}')


def profile(editor, version, variant):
    """The submodules the scene of a variant calls: an unstripped build instrumented by the stripping tool, run until
    the ready line, the report of the page taken. A submodule never called is a candidate only: the run covers the
    start and the parts of the scene, so a candidate is tried in a measured build (smoke ready, smoke.png)."""
    config, strip, _ = prepare(editor, version, variant)
    paths = Paths(version, variant)
    assemble_and_build(editor, version, paths, config, strip, ['-measurementMissing', 'Ignore', '-measurementProfile'])
    report_path = os.path.join(paths.project, 'Measurement', 'submodule-report.json')
    run = smoke(paths.output, paths.project, report_path)
    if run['status'] != 'ready' or not os.path.isfile(report_path):
        raise MeasurementError(f'profiling run {run["status"]}: {run.get("line") or run.get("errors") or run.get("reportError")}')

    submodules = read_json(report_path)['submodules']
    used = sorted(name for name, called in submodules.items() if called)
    unused = sorted(name for name, called in submodules.items() if not called)
    stripped = set(strip)
    result = {
        'unity': version,
        'variant': variant,
        'measured': datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC'),
        'smoke': run['line'],
        'candidates': [name for name in unused if name not in stripped],
        'calledButStripped': [name for name in used if name in stripped],
        'used': used,
        'unusedStripped': [name for name in unused if name in stripped],
    }
    folder = os.path.join(ROOT, 'profiles', version)
    os.makedirs(folder, exist_ok=True)
    write_json(os.path.join(folder, f'{variant}.json'), result)
    print(f'OK profile {paths.name}: {len(used)} submodules called, {len(result["candidates"])} not called and not stripped:', flush=True)
    for name in result['candidates']:
        print(f'  {name}', flush=True)
    if result['calledButStripped']:
        print('Called but stripped by the lists (kept alive by Ignore): ' + ', '.join(result['calledButStripped']), flush=True)


# ---- project ----

def with_overrides(config, version):
    """A variant may change itself for some Unity versions ("overrides": [{"unity": "6000." | "from": "7000.0.0a16",
    "why": ..., "packages": {...}, "unitypackages": [...], "keep": [...]}]): "unity" is a prefix, "from" the first version
    (by version_key). packages replace entries of the manifest (a fork of one major version does not build in another),
    a unitypackage entry takes the files under "only" from a .unitypackage of a built-in package of the editor, keep
    removes submodules from the stripping lists (a version that needs them). The applied ones go to
    the result, so a table row says what it measured."""
    config = dict(config, packages=dict(config['packages']), unitypackages=[], keep=[], applied=[])
    for override in config.get('overrides', []):
        if applies(override, version):
            config['packages'].update(override.get('packages', {}))
            config['unitypackages'] += override.get('unitypackages', [])
            config['keep'] += override.get('keep', [])
            config['applied'].append(override['why'])
    return config


def assemble(editor, project, config, strip, version):
    """The project is generated: its Assets/Measurement, manifest and the project settings of template/ProjectSettings
    (those of pixel-swarm: the built-in shaders off in GraphicsSettings, quality, physics, audio) are replaced on every
    run, Library is kept. A project without ProjectSettings is a new project to Unity, which adds the packages of its
    default template (analytics, purchasing, ...) to the manifest; ProjectVersion.txt is written before the first start."""
    os.makedirs(os.path.join(project, 'ProjectSettings'), exist_ok=True)
    shutil.copytree(os.path.join(ROOT, 'template', 'ProjectSettings'), os.path.join(project, 'ProjectSettings'), dirs_exist_ok=True)
    version_file = os.path.join(project, 'ProjectSettings', 'ProjectVersion.txt')
    if not os.path.isfile(version_file):
        with open(version_file, 'w') as file:
            file.write(f'm_EditorVersion: {version}\n')

    for folder in ('Assets/Measurement', 'Assets/Generated', 'Assets/TextMesh Pro', 'Measurement', 'Build-WebGL'):
        shutil.rmtree(os.path.join(project, folder), ignore_errors=True)

    sources = [os.path.join(ROOT, 'template')] + [os.path.join(ROOT, 'parts', part) for part in config['parts']]
    for source in sources:
        shutil.copytree(os.path.join(source, 'Assets'), os.path.join(project, 'Assets'), dirs_exist_ok=True)
    apply_profile(os.path.join(project, PROFILE), config.get('profile', {}))

    manifest = read_json(os.path.join(ROOT, 'template', 'Packages', 'manifest.json'))
    manifest['dependencies'].update(config['packages'])
    packages = os.path.join(project, 'Packages')
    # null in the packages of a variant takes the package out of the manifest of the template.
    manifest['dependencies'] = {key: package_reference(editor, key, value, packages) for key, value in sorted(manifest['dependencies'].items())
                                if value is not None}
    os.makedirs(packages, exist_ok=True)
    write_json(os.path.join(packages, 'manifest.json'), manifest)
    lock = os.path.join(packages, 'packages-lock.json')
    if os.path.isfile(lock):
        os.remove(lock)

    os.makedirs(os.path.join(project, 'Measurement'))
    with open(os.path.join(project, 'Measurement', 'strip.txt'), 'w') as file:
        file.write('\n'.join(strip) + '\n')

    return manifest


def apply_profile(path, settings):
    """A variant may change player settings of the web profile per platform ("profile": {"il2cppCodeGeneration": 1}):
    the WebGL entry of each named map of the profile copied into the project is replaced."""
    if not settings:
        return
    with open(path) as file:
        content = file.read()
    for key, value in settings.items():
        content, count = re.subn(rf'(\n  {key}:\n(?:    \w+: [^\n]*\n)*?    WebGL: )[^\n]*', rf'\g<1>{value}', content)
        if count != 1:
            raise MeasurementError(f'{PROFILE}: no WebGL entry of {key}')
    with open(path, 'w') as file:
        file.write(content)


def bootstrap(editor, project, manifest):
    """The first start of a new project (no Library) imports it and writes the settings the template has not. Unity
    7000 (a10 to a16) crashed with SIGSEGV at its end when the project had only ProjectVersion.txt, after the settings
    were written; the next start is clean. So the first start is its own run, judged by what it leaves: the settings
    written and the manifest untouched."""
    if os.path.isdir(os.path.join(project, 'Library')):
        return
    settings = os.path.join(project, 'ProjectSettings', 'EditorSettings.asset')
    log = os.path.join(project, 'bootstrap.log')
    print(f'First start of {os.path.basename(project)} (log {log})', flush=True)
    code = subprocess.run([editor, '-batchmode', '-quit', '-nographics', '-projectPath', project, '-buildTarget', 'WebGL',
                           '-logFile', log], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                          timeout=BUILD_TIMEOUT).returncode
    if not os.path.isfile(settings):
        raise MeasurementError(f'the first start (exit {code}) wrote no ProjectSettings, see {log}')
    if read_json(os.path.join(project, 'Packages', 'manifest.json')) != manifest:
        raise MeasurementError('the first start changed the manifest (packages of a new project)')


def extract_unitypackage(editor, entry, project):
    """Writes the assets of a .unitypackage under entry["only"] into the project, with their .meta (the GUIDs of the
    package). AssetDatabase.ImportPackage is asynchronous in batchmode: the build would run before the import."""
    source = os.path.join(builtin_packages(editor), entry['package'], entry['file'])
    if not os.path.isfile(source):
        raise MeasurementError(f'no {source}')
    written = 0
    with tarfile.open(source, 'r:gz') as archive:
        members = {member.name: member for member in archive.getmembers()}
        for name, member in members.items():
            if not name.endswith('/pathname'):
                continue
            folder = name[:-len('pathname')]
            path = archive.extractfile(member).read().decode().splitlines()[0]
            if not path.startswith(entry['only']) or folder + 'asset' not in members:
                continue
            target = os.path.normpath(os.path.join(project, path))
            if not target.startswith(os.path.join(project, 'Assets') + os.sep):
                raise MeasurementError(f'{source}: {path} is outside Assets')
            os.makedirs(os.path.dirname(target), exist_ok=True)
            for suffix, extension in (('asset', ''), ('asset.meta', '.meta')):
                if folder + suffix in members:
                    with open(target + extension, 'wb') as file:
                        file.write(archive.extractfile(members[folder + suffix]).read())
            written += 1
    if written == 0:
        raise MeasurementError(f'{source}: nothing under {entry["only"]}')
    print(f'{written} assets of {os.path.basename(source)} under {entry["only"]}', flush=True)


def builtin_packages(editor):
    return os.path.join(os.path.dirname(editor), 'Data', 'Resources', 'PackageManager', 'BuiltInPackages')


def package_reference(editor, name, value, packages):
    if value == BUILTIN:
        return read_json(os.path.join(builtin_packages(editor), name, 'package.json'))['version']
    if not value.startswith(FRAMEWORK_PREFIX):
        return value
    target = os.path.join(FRAMEWORK, value[len(FRAMEWORK_PREFIX):])
    if not os.path.isdir(target):
        raise MeasurementError(f'no package folder {target}')
    return 'file:' + os.path.relpath(target, packages)


def resolve_stripping(editor, files):
    """The names of the lists, checked against what the editor and the stripping tool define. An unknown name
    fails (a typo would strip nothing silently); a name marked "?" is skipped where the version has no such submodule."""
    known = known_submodules(editor)
    names, skipped = [], []

    for file in files:
        with open(os.path.join(ROOT, 'stripping', file)) as lines:
            for line in lines:
                line = line.strip()
                if not line or line.startswith('#'):
                    continue
                optional = line.startswith(OPTIONAL_PREFIX)
                name = line[len(OPTIONAL_PREFIX):].strip() if optional else line
                if name in known:
                    if name not in names:
                        names.append(name)
                elif optional:
                    skipped.append(name)
                else:
                    raise MeasurementError(f'{file}: no submodule "{name}" in {editor} or the stripping tool')

    return names, skipped


def known_submodules(editor):
    data = os.path.join(os.path.dirname(editor), 'Data', 'PlaybackEngines', 'WebGLSupport', 'BuildTools', 'lib')
    definitions = glob.glob(os.path.join(data, '*', 'submodules.json'))
    definitions += glob.glob(os.path.join(FRAMEWORK, 'client', 'packages', 'com.unity.web.stripping-tool', 'SubmoduleDefinitions', '*.json'))
    if not definitions:
        raise MeasurementError(f'no submodule definitions under {data}')

    names = set()
    for path in definitions:
        content = read_json(path)
        for submodule in content.get('submodules') or content.get('Submodules') or []:
            names.add(submodule.get('name') or submodule.get('Name'))
    return names


# ---- build ----

def build(editor, paths, flags):
    log, record_path = paths.log, paths.record
    with build_slot():
        print(f'Building {paths.name} (log {log})', flush=True)
        started = time.time()
        # -nographics: no windows of the editor on the desktop (the importer window of TextMeshPro opens when its settings
        # are read before the scene step makes them); the WebGL shaders are compiled without a device.
        command = [editor, '-batchmode', '-quit', '-nographics', '-projectPath', paths.project, '-buildTarget', 'WebGL',
                   '-executeMethod', 'Measurement.MeasurementBuild.Run', '-buildOutput', paths.output,
                   '-measurementStrip', os.path.join(paths.project, 'Measurement', 'strip.txt'),
                   '-measurementRecord', record_path, '-logFile', log] + flags
        try:
            code = subprocess.run(command, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                  timeout=BUILD_TIMEOUT).returncode
        except subprocess.TimeoutExpired:
            raise MeasurementError(f'no exit in {BUILD_TIMEOUT} s, see {log}')
        print(f'Unity exited {code} after {time.time() - started:.0f} s', flush=True)

    if code != 0 or not os.path.isfile(record_path):
        raise MeasurementError(f'exit {code}, see {log}:\n' + log_errors(log))


def log_errors(log):
    markers = ('error CS', 'Exception', 'emcc: error', 'Tundra build failed', 'Submodule stripping failed', 'Caught fatal signal')
    try:
        with open(log, errors='replace') as file:
            lines = [line.rstrip() for line in file if any(marker in line for marker in markers)]
    except FileNotFoundError:
        return '(no log)'
    return '\n'.join(lines[-20:])


class build_slot:
    """One of UNITY_BUILD_SLOTS batchmode builds of the machine, the same lock files as deploy-client."""

    def __enter__(self):
        slots = int(os.environ.get('UNITY_BUILD_SLOTS', '2'))
        os.makedirs(SLOT_DIR, exist_ok=True)
        said = 0
        while True:
            for i in reversed(range(slots)):
                self.file = open(os.path.join(SLOT_DIR, f'unity-build.slot{i}.lock'), 'w')
                try:
                    fcntl.flock(self.file, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    return self
                except BlockingIOError:
                    self.file.close()
            if time.time() - said > 60:
                print(f'Waiting for a free build slot of {slots}', flush=True)
                said = time.time()
            time.sleep(5)

    def __exit__(self, *exc):
        self.file.close()


# ---- sizes ----

def measure_files(build_dir):
    files = []
    for path in sorted(glob.glob(os.path.join(build_dir, '*'))):
        name = os.path.basename(path)
        raw = decompress(path)
        entry = {'name': name, 'kind': file_kind(name), 'compressed': os.path.getsize(path), 'raw': len(raw)}
        if entry['kind'] == 'wasm':
            entry['sections'] = wasm_sections(raw)
        if entry['kind'] == 'data':
            entry['contents'] = data_contents(raw)
        files.append(entry)
    if not files:
        raise MeasurementError(f'no files in {build_dir}')
    return files


def streaming_files(folder):
    """The files of StreamingAssets the page loads after the start (the catalog and the bundles of Addressables): served
    as they are, so compressed is the size of the file (the bundles carry their own LZ4)."""
    files = []
    for path in sorted(glob.glob(os.path.join(folder, '**', '*'), recursive=True)):
        # AddressablesLink/link.xml is an input of the IL2CPP linker the page never loads.
        if os.path.isfile(path) and os.path.basename(os.path.dirname(path)) != 'AddressablesLink':
            size = os.path.getsize(path)
            files.append({'name': os.path.relpath(path, folder), 'kind': 'streaming', 'compressed': size, 'raw': size})
    return files


def file_kind(name):
    for suffix, kind in (('.wasm', 'wasm'), ('.data', 'data'), ('.framework.js', 'framework'), ('.loader.js', 'loader'),
                         ('.symbols.json', 'symbols')):
        if suffix in name:
            return kind
    return 'other'


def decompress(path):
    with open(path, 'rb') as file:
        content = file.read()
    if path.endswith('.br') or path.endswith('.br.bak'):
        return subprocess.run(['brotli', '-d', '-c'], input=content, stdout=subprocess.PIPE, check=True).stdout
    if path.endswith('.gz') or path.endswith('.gz.bak'):
        import gzip
        return gzip.decompress(content)
    return content


WASM_SECTIONS = {0: 'custom', 1: 'type', 2: 'import', 3: 'function', 4: 'table', 5: 'memory', 6: 'global', 7: 'export',
                 8: 'start', 9: 'element', 10: 'code', 11: 'data', 12: 'datacount', 13: 'tag'}


def wasm_sections(content):
    """Bytes of each section of a wasm module: code is the compiled engine and IL2CPP code, data its constants
    (IL2CPP metadata of the types and strings goes to the .data file, not here)."""
    if content[:4] != b'\0asm':
        raise MeasurementError('not a wasm module')
    sections, offset = {}, 8
    while offset < len(content):
        section = content[offset]
        size, offset = leb128(content, offset + 1)
        key = WASM_SECTIONS.get(section, f'section{section}')
        sections[key] = sections.get(key, 0) + size
        offset += size
    return sections


def leb128(content, offset):
    result = shift = 0
    while True:
        byte = content[offset]
        offset += 1
        result |= (byte & 0x7f) << shift
        shift += 7
        if byte & 0x80 == 0:
            return result, offset


def data_contents(content):
    """The files of the UnityWebData container (.data): the scene and assets (data.unity3d, sharedassets, unity default
    resources), the IL2CPP metadata, the ICU data of the BCL, the resources of the assemblies. brotli is an estimate:
    each file compressed alone with the quality of the build, so the parts do not add up to the file exactly."""
    magic = b'UnityWebData1.0\0'
    if not content.startswith(magic):
        raise MeasurementError('the .data file is not a UnityWebData1.0 container')
    offset = len(magic)
    header = struct.unpack_from('<I', content, offset)[0]
    offset += 4
    contents = []
    while offset < header:
        start, size, length = struct.unpack_from('<III', content, offset)
        offset += 12
        name = content[offset:offset + length].decode()
        offset += length
        compressed = subprocess.run(['brotli', '-c', '-q', '11'], input=content[start:start + size], stdout=subprocess.PIPE,
                                    check=True).stdout
        contents.append({'name': name, 'raw': size, 'compressed': len(compressed)})
    return contents


def wasm_before_stripping(project):
    """The wasm as the IL2CPP and emscripten build left it, before the submodule stripping: the tool keeps it in
    Library/com.unity.web.stripping-tool/<build>/ as *.wasm*.bak (the latest build of the project)."""
    backups = glob.glob(os.path.join(project, 'Library', 'com.unity.web.stripping-tool', '*', '*.wasm*.bak'))
    if not backups:
        raise MeasurementError('no wasm backup of the stripping tool: did the submodule stripping run?')
    latest = max(backups, key=os.path.getmtime)
    return {'compressed': os.path.getsize(latest), 'raw': len(decompress(latest))}


def managed_assemblies(project):
    """The assemblies after the IL2CPP linker (managed stripping High): what the .NET libraries cost before C++."""
    folders = glob.glob(os.path.join(project, 'Library', 'Bee', 'artifacts', '**', 'ManagedStripped'), recursive=True)
    if not folders:
        raise MeasurementError('no ManagedStripped folder in Library/Bee/artifacts')
    folder = max(folders, key=os.path.getmtime)
    # Unity 6 writes them into the folder, Unity 7000 into ManagedStripped/wasm.
    assemblies = {os.path.basename(path): os.path.getsize(path) for path in glob.glob(os.path.join(folder, '**', '*.dll'), recursive=True)}
    if not assemblies:
        raise MeasurementError(f'no assemblies in {folder}')
    return dict(sorted(assemblies.items(), key=lambda pair: -pair[1]))


# ---- smoke ----

def smoke(output, project, report=None):
    """Runs the build in headless Chromium until the scene logs its ready line (tools/smoke.mjs); with report the
    submodule report of an instrumented build is written there."""
    screenshot = os.path.join(project, 'smoke.png')
    run = subprocess.run(['node', os.path.join(TOOLS, 'smoke.mjs'), output, screenshot] + ([report] if report else []), stdout=subprocess.PIPE,
                         stderr=subprocess.PIPE, text=True, timeout=300)
    if run.returncode != 0:
        return {'status': 'crashed', 'errors': [run.stderr.strip()[-2000:]]}
    return json.loads(run.stdout)


# ---- files ----

def read_json(path):
    with open(path) as file:
        return json.load(file)


def write_json(path, content):
    with open(path, 'w') as file:
        json.dump(content, file, indent=2)
        file.write('\n')


if __name__ == '__main__':
    main()
