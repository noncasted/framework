#!/usr/bin/env python3
"""Writes the submodule definitions of the code no measured scene calls: engine_unused.json and bcl_unused.json of the
stripping tool fork.

Unity's definitions leave some engine code and the .NET BCL outside any submodule. Each group below is such code: the
engine groups by a pattern of the names of the wasm functions (the name section of the build before the stripping), the
BCL groups by an assembly and a pattern of the C# signatures of the MethodMap of IL2CPP. A function goes into the
definition when its body has at least MIN_SIZE bytes and no called.txt of the given projects (tools/coverage.py) lists
it. The small ones stay: the linker folds identical functions under one name, and a small body is the one likely shared
with code outside the group. A group is here only where the measured scenes call nothing of it but its startup
registration and the reason holds for a game too (the description of each).

Usage: tools/unused_definition.py <unity version> <project> [project ...]
  the first project gives the names (its debug wasm and MethodMap), every project its called.txt
  e.g. tools/unused_definition.py 7000.0.0a20 projects/7000.0.0a20-aggressive-lean projects/7000.0.0a20-aggressive-lean-assetbundles
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMEWORK = os.path.dirname(os.path.dirname(ROOT))
DEFINITIONS = os.path.join(FRAMEWORK, 'client', 'packages', 'com.unity.web.stripping-tool', 'SubmoduleDefinitions')
MIN_SIZE = 200

ENGINE_GROUPS = [
    ('Safe Binary Read', r'SafeBinaryRead',
     'The reading of serialized data whose type tree differs from the type of the player (old AssetBundles with type '
     'trees): the data of a player build and the bundles of ContentBuild (no type trees) are read by StreamedBinaryRead.'),
    ('Graphics State Collection', r'GraphicsStateCollection',
     'The GraphicsStateCollection API (recording and warming up the graphics states of the shaders): only the module '
     'registration runs unless a game calls the API.'),
    ('Platform Audio', r'^PlatformAudio::',
     'The audio devices and format conversion of the native platforms: the web player plays through the WebAudio code.'),
    ('Content Load Backend', r'^ContentLoadBackend::',
     'The loading of content files (ContentFileManager / content directories): scenes and AssetBundles do not use it.'),
    ('Text From Binary Renderer', r'^TextFromBinaryRenderer::',
     'The text dump of binary serialized data: an editor and debugging path.'),
    ('Content Load Manager', r'^ContentLoad::(ContentLoadManager|ContentDirectory)::',
     'The content files and content directories of the Unity.Loading API (ContentLoadManager.LoadContentFileAsync, the '
     'scenes of a content directory): SceneManager and AssetBundles load without it, no game of the framework calls the API.'),
]

BCL_GROUPS = [
    ('BCL Event Source', [
        ('System.Private.CoreLib', r'^\S+ System\.Diagnostics\.Tracing\.'),
        ('System.Private.CoreLib', r'^\S+ System\.Threading\.Tasks\.TplEventSource[/:]'),
        ('System.Private.CoreLib', r'^\S+ System\.Buffers\.ArrayPoolEventSource[/:]'),
        ('System.Collections.Concurrent', r'^\S+ System\.Collections\.Concurrent\.CDSCollectionETWBCLProvider[/:]'),
    ], 'EventSource and the event sources of the BCL: UnityLinker sets System.Diagnostics.Tracing.EventSource.IsSupported '
       'to false for IL2CPP, so no event source is ever enabled, but EventSource keeps all its methods (DynamicallyAccessedMembers '
       'on the type) once a source is created (ConcurrentDictionary, Task, ArrayPool). Their constructors run and stay.'),
    ('BCL JavaScript Interop', [
        ('System.Runtime.InteropServices.JavaScript', r'.'),
    ], 'JSImport / JSExport of .NET on the browser, kept by the descriptor of CoreLib: Unity calls JavaScript through its '
       '.jslib plugins.'),
    ('BCL Resources', [
        ('System.Private.CoreLib', r'^\S+ System\.Resources\.'),
        ('System.Private.CoreLib', r'^\S+ System\.SR::InternalGetResourceString\('),
    ], 'The lookup of the resource strings of the BCL (ResourceManager, ResourceReader, the resource sets). Only for a build '
       'with the feature UseSystemResourceKeys: UnityLinker then sets SR.s_usingResourceKeys to true in its static '
       'constructor, so SR.GetResourceString returns the key and never reaches the lookup (whose resources the linker removed).'),
]


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    version = sys.argv[1]
    projects = [os.path.abspath(path) for path in sys.argv[2:]]
    wasm = os.path.join(projects[0], 'Library', 'Bee', 'artifacts', 'WebGL', 'build', 'debug_WebGL_wasm', 'build.wasm')
    sizes = function_sizes(wasm)
    called = set()
    for project in projects:
        path = os.path.join(project, 'called.txt')
        if not os.path.exists(path):
            sys.exit(f'no {path}: run tools/coverage.py for it')
        with open(path) as file:
            called.update(line.rstrip('\n') for line in file)

    keep = lambda name: sizes.get(name, 0) >= MIN_SIZE and name not in called
    engine = [(name, why, sorted(n for n in sizes if re.search(pattern, n) and keep(n))) for name, pattern, why in ENGINE_GROUPS]
    methods = method_map(os.path.join(projects[0], 'Library', 'Bee', 'artifacts', 'WebGL', 'il2cppOutput', 'wasm', 'cpp', 'Symbols', 'MethodMap.tsv'))
    bcl = [(name, why, sorted({native for assembly, pattern in filters for native, signature, owner in methods
                               if owner == assembly and re.search(pattern, signature) and keep(native)}))
           for name, filters, why in BCL_GROUPS]
    write('engine_unused.json', engine, sizes, version)
    write('bcl_unused.json', bcl, sizes, version)


def write(file_name, groups, sizes, version):
    submodules = []
    for name, why, functions in groups:
        print(f'{name}: {len(functions)} functions, {sum(sizes[n] for n in functions) // 1024} KiB')
        submodules.append({
            'name': name,
            'description': f'{why} Functions of at least {MIN_SIZE} bytes that no measured scene calls; names of Unity {version}, '
                           'written by client/measurements/tools/unused_definition.py.',
            'functions': functions,
        })
    path = os.path.join(DEFINITIONS, file_name)
    with open(path, 'w') as file:
        json.dump({'submodules': submodules}, file, indent=2)
        file.write('\n')
    print(f'-> {path}')


def method_map(path):
    """(native function name, C# signature, assembly) of every method of the MethodMap.tsv of IL2CPP."""
    with open(path, encoding='utf-8', errors='replace') as file:
        return [tuple(line.rstrip('\n').split('\t')[:3]) for line in file if line.count('\t') >= 2]


def function_sizes(path):
    """The size of the body of every named function of a wasm module with a name section (the largest of equal names)."""
    with open(path, 'rb') as file:
        content = file.read()
    offset, imports, bodies, names = 8, 0, [], {}
    while offset < len(content):
        section = content[offset]
        size, offset = leb128(content, offset + 1)
        end = offset + size
        if section == 2:
            imports = count_function_imports(content, offset)
        elif section == 10:
            count, position = leb128(content, offset)
            for _ in range(count):
                length, start = leb128(content, position)
                bodies.append(length)
                position = start + length
        elif section == 0:
            length, position = leb128(content, offset)
            if content[position:position + length] == b'name':
                position += length
                while position < end:
                    kind = content[position]
                    subsize, position = leb128(content, position + 1)
                    if kind == 1:
                        count, cursor = leb128(content, position)
                        for _ in range(count):
                            index, cursor = leb128(content, cursor)
                            length, cursor = leb128(content, cursor)
                            names[index] = content[cursor:cursor + length].decode('utf-8', 'replace')
                            cursor += length
                    position += subsize
        offset = end
    if not names:
        sys.exit(f'no name section in {path}')
    sizes = {}
    for i, size in enumerate(bodies):
        name = names.get(imports + i)
        if name is not None:
            sizes[name] = max(size, sizes.get(name, 0))
    return sizes


def count_function_imports(content, offset):
    count, position = leb128(content, offset)
    functions = 0
    for _ in range(count):
        for _ in range(2):
            length, position = leb128(content, position)
            position += length
        kind = content[position]
        position += 1
        if kind == 0:
            _, position = leb128(content, position)
            functions += 1
        elif kind in (1, 2):
            if kind == 1:
                position += 1
            flags, position = leb128(content, position)
            _, position = leb128(content, position)
            if flags & 1:
                _, position = leb128(content, position)
        elif kind == 3:
            position += 2
        elif kind == 4:
            position += 1
            _, position = leb128(content, position)
    return functions


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
