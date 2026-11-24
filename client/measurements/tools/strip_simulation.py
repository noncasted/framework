#!/usr/bin/env python3
"""Repeats the submodule stripping of a measured build outside Unity, keeping the names: which functions survive it,
and how large each is. Writes projects/<version>-<variant>/StripSimulation/stripped.wasm (with its name section) and
survivors.tsv (size of the body, name; the largest first).

The input is the wasm the stripping tool started from (Library/com.unity.web.stripping-tool/<build>/*.wasm.br.bak),
the functions are those of the submodules its stripping_info.json lists as stripped (the definitions of the editor,
lib/modules_wasm23/submodules.json, and of the fork, SubmoduleDefinitions/*.json; a composite submodule is the union
of its parts), escaped as the tool does, and the passes are those of WasmOpt.RemoveFunctions and WasmOpt.Optimize of
the fork with -g. The C# method filters of a definition are not evaluated: a definition that has one is reported.

Usage: tools/strip_simulation.py <unity version> <variant>
"""
import glob
import json
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wasm_binary as wb  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FORK = os.path.join(os.path.dirname(ROOT), 'packages', 'com.unity.web.stripping-tool', 'SubmoduleDefinitions')
FEATURES = ['--enable-simd', '--enable-exception-handling', '--enable-bulk-memory', '--enable-bulk-memory-opt',
            '--enable-nontrapping-float-to-int']
OPTIMIZE = ['-Oz', '--inlining-optimizing', '--dce', '--coalesce-locals', '--code-folding', '--code-pushing', '--const-hoisting',
            '--dae-optimizing', '--duplicate-function-elimination', '--local-cse', '--once-reduction', '--optimize-for-js',
            '--optimize-instructions', '--optimize-stack-ir', '--post-emscripten', '--merge-blocks', '--merge-locals',
            '--merge-similar-functions', '--remove-unused-brs', '--remove-unused-names', '--remove-unused-module-elements',
            '--remove-unused-nonfunction-module-elements', '--reorder-locals', '--rse', '--traps-never-happen']


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    version, variant = sys.argv[1:]
    project = os.path.join(ROOT, 'projects', f'{version}-{variant}')
    editor = os.path.join(os.environ.get('UNITY_EDITORS', '/tools/unity'), version, 'Editor')
    binaryen = os.path.join(editor, 'Data', 'PlaybackEngines', 'WebGLSupport', 'BuildTools', 'Emscripten', 'binaryen', 'bin')
    folders = glob.glob(os.path.join(project, 'Library', 'com.unity.web.stripping-tool', '*'))
    if len(folders) != 1:
        sys.exit(f'expected one build folder of the stripping tool in {project}/Library, found {folders}')
    info = json.load(open(os.path.join(folders[0], 'stripping_info.json')))
    sources = glob.glob(os.path.join(folders[0], '*.wasm.br.bak'))
    if len(sources) != 1:
        sys.exit(f'expected one .wasm.br.bak in {folders[0]}, found {sources}')

    definitions = {}
    for path in [os.path.join(editor, 'Data', 'PlaybackEngines', 'WebGLSupport', 'BuildTools', 'lib', 'modules_wasm23', 'submodules.json')] + \
            sorted(glob.glob(os.path.join(FORK, '*.json'))):
        for definition in json.load(open(path))['submodules']:
            definitions.setdefault(definition['name'], definition)

    def functions(name):
        definition = definitions[name]
        if definition.get('csharpMethodFilters'):
            print(f'warning: {name} has C# method filters, not evaluated here', file=sys.stderr)
        result = list(definition.get('functions') or [])
        for part in definition.get('submodules') or []:
            result += functions(part)
        return result

    names = sorted({name for submodule in info['strippedSubmodules'] for name in functions(submodule)})
    folder = os.path.join(project, 'StripSimulation')
    os.makedirs(folder, exist_ok=True)
    source = os.path.join(folder, 'source.wasm')
    with open(source, 'wb') as file:
        subprocess.run(['brotli', '-d', '-c', sources[0]], stdout=file, check=True)
    ids = os.path.join(folder, 'ids.txt')
    with open(ids, 'w') as file:
        # One id per line, as FunctionListWriter of the fork writes them (wasm-opt reads the file line by line).
        file.write('\n'.join(name.replace('(', '\\28').replace(')', '\\29').replace(' ', '\\20').replace(',', '\\2c') for name in names))
    removed = os.path.join(folder, 'removed.wasm')
    wasm_opt = os.path.join(binaryen, 'wasm-opt')
    subprocess.run([wasm_opt, source, '-o', removed, f'--remove-functions=@{ids}', '-g'] + FEATURES + ['-n'], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    stripped = os.path.join(folder, 'stripped.wasm')
    subprocess.run([wasm_opt, removed, '-o', stripped, '-g'] + OPTIMIZE + FEATURES + ['-n'], check=True)
    os.remove(source)
    os.remove(removed)

    module = wb.Module(open(stripped, 'rb').read())
    function_names = module.function_names()
    survivors = sorted(((stop - start, function_names.get(index, str(index))) for index, start, stop in module.bodies()), reverse=True)
    with open(os.path.join(folder, 'survivors.tsv'), 'w') as file:
        file.writelines(f'{size}\t{name}\n' for size, name in survivors)
    print(f'{os.path.basename(project)}: {len(names)} functions of {len(info["strippedSubmodules"])} submodules removed, '
          f'{len(survivors)} functions survive ({sum(size for size, _ in survivors) // 1024} KiB of bodies) -> {folder}')


if __name__ == '__main__':
    main()
