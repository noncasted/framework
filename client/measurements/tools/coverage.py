#!/usr/bin/env python3
"""Writes which wasm functions a measured build calls in its headless run: projects/<version>-<variant>/called.txt.

The wasm of the build before the submodule stripping (Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.wasm,
with its name section) is instrumented by `wasm-split --instrument` of the emscripten of the editor (a flag per function,
set on its first call), put into a copy of Build-WebGL (Coverage-WebGL) in place of the stripped wasm, and run by
tools/smoke.mjs --wasm-profile until the ready line and three seconds more. `wasm-split --print-profile` reads the
profile back; called.txt lists the names of the called functions, one per line.

Run it right after tools/measure.py of the same variant: the instrumented wasm is the one of that build. It measures
the scene only: a function it does not list may be called by a game. Functions folded by the linker (identical code
of several symbols) carry one of their names, so a name in the list may stand for the code of another one too.

Usage: tools/coverage.py <unity version> <variant> [variant ...]
"""
import glob
import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOLS = os.path.join(ROOT, 'tools')


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    version = sys.argv[1]
    binaryen = os.path.join(os.environ.get('UNITY_EDITORS', '/tools/unity'), version, 'Editor', 'Data', 'PlaybackEngines', 'WebGLSupport',
                            'BuildTools', 'Emscripten', 'binaryen', 'bin')
    for variant in sys.argv[2:]:
        coverage(binaryen, os.path.join(ROOT, 'projects', f'{version}-{variant}'))


def coverage(binaryen, project):
    wasm = os.path.join(project, 'Library', 'Bee', 'artifacts', 'WebGL', 'build', 'debug_WebGL_wasm', 'build.wasm')
    build = os.path.join(project, 'Build-WebGL')
    if not os.path.exists(wasm) or not os.path.isdir(build):
        sys.exit(f'no build in {project}: run tools/measure.py first')
    copy = os.path.join(project, 'Coverage-WebGL')
    shutil.rmtree(copy, ignore_errors=True)
    shutil.copytree(build, copy)
    shipped = glob.glob(os.path.join(copy, 'Build', '*.wasm.br'))
    if len(shipped) != 1:
        sys.exit(f'expected one .wasm.br in {copy}/Build, found {shipped}')
    instrumented = os.path.join(project, 'coverage.wasm')
    subprocess.run([os.path.join(binaryen, 'wasm-split'), '--instrument', '--all-features', '-g', wasm, '-o', instrumented], check=True)
    with open(shipped[0], 'wb') as output:
        subprocess.run(['brotli', '-q', '5', '-c', instrumented], stdout=output, check=True)

    profile = os.path.join(project, 'coverage.profile')
    if os.path.exists(profile):
        os.remove(profile)
    run = subprocess.run(['node', os.path.join(TOOLS, 'smoke.mjs'), copy, os.path.join(project, 'coverage.png'), '--wasm-profile', profile],
                         stdout=subprocess.PIPE, text=True)
    if not os.path.exists(profile):
        sys.exit(f'no profile from the run of {copy}: {run.stdout[-2000:]}')

    printed = subprocess.run([os.path.join(binaryen, 'wasm-split'), '--print-profile=' + profile, '--all-features', '-u', wasm],
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, check=True).stdout
    # "+ <name>" for a called function; a function without a name in the name section is printed as its index.
    called = sorted({line[2:] for line in printed.splitlines() if line.startswith('+ ') and not line[2:].isdigit()})
    with open(os.path.join(project, 'called.txt'), 'w') as file:
        file.write('\n'.join(called) + '\n')
    os.remove(instrumented)
    shutil.rmtree(copy)
    print(f'{os.path.basename(project)}: {len(called)} called functions -> called.txt')


if __name__ == '__main__':
    main()
