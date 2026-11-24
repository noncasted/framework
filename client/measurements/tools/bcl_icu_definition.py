#!/usr/bin/env python3
"""Writes the submodule definition "BCL Globalization ICU" of the stripping tool fork from a built measurement project.

The ICU of the .NET BCL of IL2CPP (il2cpp/external/icu, compiled as icuuc and icui18n) and System.Globalization.Native
are not covered by the definitions of Unity (its "ICU" submodule is the ICU of the engine, icu_73::). The functions are
the symbols the object files of these three programs define (llvm-nm of the emscripten of the editor), as the name
section of the linked wasm spells them; a Unity version with another ICU needs the definition written again.

Usage: tools/bcl_icu_definition.py <unity version> <project folder>
  e.g. tools/bcl_icu_definition.py 7000.0.0a20 projects/7000.0.0a20-aggressive-invariant
Writes client/packages/com.unity.web.stripping-tool/SubmoduleDefinitions/bcl_globalization.json.
"""
import glob
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMEWORK = os.path.dirname(os.path.dirname(ROOT))
DEFINITION = os.path.join(FRAMEWORK, 'client', 'packages', 'com.unity.web.stripping-tool', 'SubmoduleDefinitions', 'bcl_globalization.json')
PROGRAMS = ('icuuc', 'icui18n', 'systemglobalizationnative')
NAME = 'BCL Globalization ICU'


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    version, project = sys.argv[1], os.path.abspath(sys.argv[2])
    llvm = os.path.join(os.environ.get('UNITY_EDITORS', '/tools/unity'), version, 'Editor', 'Data', 'PlaybackEngines', 'WebGLSupport',
                        'BuildTools', 'Emscripten', 'llvm')

    objects = [path for program in PROGRAMS
               for path in glob.glob(os.path.join(project, 'Library', 'Bee', 'artifacts', 'WebGL', program, '**', '*.o'), recursive=True)]
    if not objects:
        sys.exit(f'no object files of {", ".join(PROGRAMS)} in {project}: build the project first')
    mangled = subprocess.run([os.path.join(llvm, 'llvm-nm'), '--defined-only', '-j'] + objects, stdout=subprocess.PIPE,
                             stderr=subprocess.DEVNULL, text=True, check=True).stdout
    symbols = set(subprocess.run([os.path.join(llvm, 'llvm-cxxfilt')], input=mangled, stdout=subprocess.PIPE, text=True,
                                 check=True).stdout.splitlines())

    wasm = os.path.join(project, 'Library', 'Bee', 'artifacts', 'WebGL', 'build', 'debug_WebGL_wasm', 'build.wasm')
    functions = sorted(name for name in function_names(wasm) if name in symbols)
    if not functions:
        sys.exit(f'no function of the object files in the name section of {wasm}')

    definition = {'submodules': [{
        'name': NAME,
        'description': 'The ICU of the .NET BCL of IL2CPP (il2cpp/external/icu: icuuc, icui18n) and System.Globalization.Native: '
                       'unused when the BCL runs in the invariant globalization mode (IL2CPP_SYSTEM_GLOBALIZATION_INVARIANT, '
                       f'System.Globalization.Invariant). Names of Unity {version}, written by client/measurements/tools/bcl_icu_definition.py.',
        'functions': functions,
    }]}
    with open(DEFINITION, 'w') as file:
        json.dump(definition, file, indent=2)
        file.write('\n')
    print(f'{len(functions)} functions -> {DEFINITION}')


def function_names(path):
    """The names of the functions of the name section of a wasm module (the build keeps it for the stripping tool)."""
    with open(path, 'rb') as file:
        content = file.read()
    offset, names = 8, []
    while offset < len(content):
        section = content[offset]
        size, offset = leb128(content, offset + 1)
        end = offset + size
        if section == 0:
            length, position = leb128(content, offset)
            if content[position:position + length] == b'name':
                position += length
                while position < end:
                    kind = content[position]
                    subsize, position = leb128(content, position + 1)
                    if kind == 1:
                        count, cursor = leb128(content, position)
                        for _ in range(count):
                            _, cursor = leb128(content, cursor)
                            length, cursor = leb128(content, cursor)
                            names.append(content[cursor:cursor + length].decode('utf-8', 'replace'))
                            cursor += length
                    position += subsize
        offset = end
    if not names:
        raise SystemExit(f'no name section in {path}')
    return names


def leb128(content, offset):
    result = shift = 0
    while True:
        byte = content[offset]
        offset += 1
        result |= (byte & 0x7f) << shift
        shift += 7
        if byte & 0x80 == 0:
            return result, offset


if __name__ == '__main__':
    main()
