#!/usr/bin/env python3
"""Zeroes the data of the wasm of a measured build that no code left after the submodule stripping can reach, and
measures what it saves: projects/<version>-<variant>/DeadData/.

The stripping tool removes functions, never their data: the tables of ICU, of the texture decoders, the strings of
stripped code stay in the data section. To know which data is still reachable the player is linked again from the
inputs of the build (the Link_WebGL_wasm action of the Bee graph, EMCC_DEBUG keeps the output of wasm-ld) with
--emit-relocs: every address of a data symbol in the code and in the data is then a relocation to that symbol. The
layout of the data is the one of the build (checked byte for byte against the shipped wasm).

A data symbol is live when
  - a constant of the code of the shipped wasm (an i32.const, the offset of a memory access, a global initializer) is
    the address of one of its relocations, or falls inside it while the symbol has relocations in the code, unless
    the value is a literal of the code before the post-link optimization (a constant at no relocation, such as
    0x10FFFF or 1000000, also negated and moved by one as binaryen rewrites them) further than WINDOW bytes from
    every relocation of the symbol (binaryen folds a field offset into the address: vtable + 8, so a value inside the
    symbol is an address too);
  - a live symbol holds a relocation to it.
Bytes of the symbols that are not live and of no live symbol are zeroed in a copy of the shipped wasm (the addresses
do not move). Bytes covered by no symbol stay.

Usage: tools/dead_data.py <unity version> <variant> [--smoke]
  writes DeadData/build.wasm and dead.txt (the dead symbols; the ones decided by the literal rule first) and prints
  the sizes; the build stays as it is (measure.py zeroes it with the variant option "zeroDeadData": true)
  --smoke   run tools/smoke.mjs on a copy of Build-WebGL with the zeroed wasm
"""
import bisect
import collections
import glob
import json
import os
import shlex
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wasm_binary as wb  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOLS = os.path.join(ROOT, 'tools')
WINDOW = 256


class DeadDataError(Exception):
    pass


def main():
    arguments = [argument for argument in sys.argv[1:] if not argument.startswith('--')]
    if len(arguments) != 2:
        sys.exit(__doc__)
    version, variant = arguments
    project = os.path.join(ROOT, 'projects', f'{version}-{variant}')
    try:
        shipped = shipped_wasm(project)
        original = subprocess.run(['brotli', '-d', '-c', shipped], stdout=subprocess.PIPE, check=True).stdout
        zeroed, stats = analyse(project, original)
        output = os.path.join(project, 'DeadData', 'build.wasm')
        with open(output, 'wb') as file:
            file.write(zeroed)
        print(json.dumps(stats))
        if '--smoke' in sys.argv:
            smoke(project, shipped, output)
    except DeadDataError as error:
        sys.exit(str(error))


def zero_build(project):
    """The post-build step of measure.py (variant option zeroDeadData): the .wasm.br of Build-WebGL is replaced by the
    zeroed wasm, compressed with brotli -q 11 as the other post-build steps. Returns the numbers for the result."""
    shipped = shipped_wasm(project)
    original = subprocess.run(['brotli', '-d', '-c', shipped], stdout=subprocess.PIPE, check=True).stdout
    zeroed, stats = analyse(project, original)
    compressed = subprocess.run(['brotli', '-c', '-q', '11'], input=zeroed, stdout=subprocess.PIPE, check=True).stdout
    with open(shipped, 'wb') as file:
        file.write(compressed)
    print(f'Zeroed {stats["deadBytes"] // 1024} KiB of dead data in {os.path.basename(shipped)}: brotli -q 11 '
          f'{stats["brotliBefore"] // 1024} -> {stats["brotliAfter"] // 1024} KiB (list in DeadData/dead.txt)', flush=True)
    return stats


def shipped_wasm(project):
    shipped = glob.glob(os.path.join(project, 'Build-WebGL', 'Build', '*.wasm.br'))
    if len(shipped) != 1:
        raise DeadDataError(f'expected one .wasm.br in {project}/Build-WebGL/Build, found {shipped}')
    return shipped[0]


def analyse(project, original):
    """The shipped wasm with its dead data zeroed, and the numbers; writes DeadData/dead.txt."""
    folder = os.path.join(project, 'DeadData')
    os.makedirs(folder, exist_ok=True)
    base = wb.Module(open(relink(project, folder), 'rb').read())
    final = wb.Module(original)
    symbols, end = data_symbols(base)
    check_layout(base, final, end)
    live, literal_hits = live_symbols(base, final, symbols, end)
    zeroed, dead = zero(final, symbols, live, end)
    stats = report(folder, symbols, live, dead, literal_hits, original, zeroed)
    return zeroed, stats


def relink(project, folder):
    """The output of wasm-ld of the Link_WebGL_wasm action with --emit-relocs, kept by EMCC_DEBUG."""
    base = os.path.join(folder, 'tmp', 'emscripten_temp', 'emcc-01-base.wasm')
    graphs = glob.glob(os.path.join(project, 'Library', 'Bee', 'Player*.dag.json'))
    if len(graphs) != 1:
        raise DeadDataError(f'expected one Player*.dag.json in {project}/Library/Bee, found {graphs}')
    nodes = [node for node in json.load(open(graphs[0]))['Nodes'] if node.get('Annotation', '').startswith('Link_WebGL_wasm')]
    if len(nodes) != 1:
        raise DeadDataError(f'expected one Link_WebGL_wasm node in {graphs[0]}')
    node = nodes[0]
    wasm = os.path.join(project, 'Library', 'Bee', 'artifacts', 'WebGL', 'build', 'debug_WebGL_wasm', 'build.wasm')
    if os.path.exists(base) and os.path.getmtime(base) > os.path.getmtime(wasm):
        return base

    shutil.rmtree(os.path.join(folder, 'tmp'), ignore_errors=True)
    os.makedirs(os.path.join(folder, 'tmp'))
    tokens = shlex.split(node['Action'])
    output = tokens.index('-o')
    tokens[output + 1] = os.path.join(folder, 'relink', 'build.js')
    tokens.insert(output, '-Wl,--emit-relocs')
    # A link map of the variant would be written again by this link.
    tokens = [token for token in tokens if not token.startswith('-Wl,-Map=')]
    os.makedirs(os.path.join(folder, 'relink'), exist_ok=True)
    environment = dict(os.environ, **{entry['Key']: entry['Value'] for entry in node.get('Env', [])})
    environment.update(EMCC_DEBUG='1', TMPDIR=os.path.join(folder, 'tmp'), EMCC_TEMP_DIR=os.path.join(folder, 'tmp'))
    log = os.path.join(folder, 'relink.log')
    print(f'Linking {os.path.basename(project)} again with --emit-relocs (log {log})', flush=True)
    with open(log, 'w') as file:
        subprocess.run(tokens, cwd=project, env=environment, stdout=file, stderr=subprocess.STDOUT, check=True)
    if not os.path.exists(base):
        raise DeadDataError(f'the link kept no {base}')
    # Only the output of wasm-ld is used: the rest of the temporary files and the post-linked copy go.
    for path in glob.glob(os.path.join(folder, 'tmp', 'emscripten_temp', '*')):
        if path != base:
            os.remove(path)
    shutil.rmtree(os.path.join(folder, 'relink'))
    return base


def data_symbols(base):
    """[(address, size, name)] of the defined data symbols with bytes in the initialized data (not .bss), and the
    end of that data."""
    segments = base.data_segments()
    # wasm-ld writes .rodata, .data and, with --emit-relocs, .bss as segments in this order.
    if len(segments) != 3:
        raise DeadDataError(f'expected the segments .rodata, .data, .bss in the relinked wasm, found {len(segments)}')
    end = segments[2][0]
    symbols = {}
    for index, (name, segment, offset, size) in base.data_symbols().items():
        address = segments[segment][0] + offset
        if size > 0 and address < end:
            symbols[index] = (address, size, name)
    return symbols, end


def check_layout(base, final, end):
    image = bytearray(end)
    for address, start, stop in base.data_segments()[:2]:
        image[address:address + stop - start] = base.data[start:stop]
    shipped = bytearray(end)
    for address, start, stop in final.data_segments():
        if address + stop - start > end:
            raise DeadDataError(f'a data segment of the shipped wasm ends at {address + stop - start:#x}, past the data ({end:#x})')
        shipped[address:address + stop - start] = final.data[start:stop]
    if image != shipped:
        first = next(index for index in range(end) if image[index] != shipped[index])
        raise DeadDataError(f'the data of the relinked wasm differs from the shipped one at {first:#x}: not the same link')


def live_symbols(base, final, symbols, end):
    code_start = base.section(10)[2]
    data_start = base.section(11)[2]

    references = collections.defaultdict(set)  # symbol -> addresses of its relocations in the code
    sites = set()
    for kind, offset, index, addend in base.relocations('reloc.CODE'):
        if kind in wb.MEMORY_ADDRESS_TYPES and index in symbols:
            references[index].add(symbols[index][0] + addend)
            sites.add(code_start + offset)

    literals = set()
    for _, start, stop in base.bodies():
        wb.instructions(base.data, start, stop, lambda value, position: position in sites or literals.add(value))
    # binaryen rewrites x + -c as x - c (the MD5 constant 0xFFEFF47D becomes 1051523) and moves the bound of a
    # comparison by one (x < c as x <= c - 1).
    literals = {(sign * value + delta) & 0xFFFFFFFF for value in literals for sign in (1, -1) for delta in (-1, 0, 1)}

    constants = set()
    for _, start, stop in final.bodies():
        wb.instructions(final.data, start, stop, lambda value, position: constants.add(value))
    constants.update(value & 0xFFFFFFFF for value in final.global_constants())
    constants = sorted(value for value in constants if value < end)

    roots = set()
    literal_hits = {}  # symbol -> the literals inside it that did not make it live
    for index, addresses in references.items():
        address, size, _ = symbols[index]
        for reference in addresses:
            at = bisect.bisect_left(constants, reference)
            if at < len(constants) and constants[at] == reference:
                roots.add(index)
                break
        else:
            low, high = bisect.bisect_left(constants, address), bisect.bisect_left(constants, address + size)
            for value in constants[low:high]:
                if value not in literals or min(abs(value - reference) for reference in addresses) < WINDOW:
                    roots.add(index)
                    break
            else:
                if high > low:
                    literal_hits[index] = constants[low:high]

    # Data relocations: the symbols that hold the site point to the target.
    segments = base.data_segments()
    holders = covering(symbols, [segment_address(segments, data_start + offset)
                                 for kind, offset, index, _ in base.relocations('reloc.DATA') if kind in wb.MEMORY_ADDRESS_TYPES])
    edges = collections.defaultdict(set)
    for kind, offset, index, _ in base.relocations('reloc.DATA'):
        if kind not in wb.MEMORY_ADDRESS_TYPES or index not in symbols:
            continue
        site = segment_address(segments, data_start + offset)
        owners = holders[site]
        if not owners and site < end:
            roots.add(index)  # a pointer in bytes of no symbol: they stay, so does its target
        for owner in owners:
            edges[owner].add(index)

    live = set(roots)
    stack = list(roots)
    while stack:
        for target in edges.get(stack.pop(), ()):
            if target not in live:
                live.add(target)
                stack.append(target)
    return live, {index: values for index, values in literal_hits.items() if index not in live}


def segment_address(segments, position):
    for address, start, stop in segments:
        if start <= position < stop:
            return address + position - start
    raise DeadDataError(f'a data relocation at {position} is in no segment')


def covering(symbols, points):
    """Point -> indices of the symbols whose bytes hold it (a sweep: the symbols overlap only as aliases and tails
    of merged strings)."""
    events = []
    for index, (address, size, _) in symbols.items():
        events.append((address, 0, index))
        events.append((address + size, -1, index))
    for point in set(points):
        events.append((point, 1, point))
    events.sort()
    active = set()
    result = {}
    for _, kind, value in events:
        if kind == 0:
            active.add(value)
        elif kind == -1:
            active.discard(value)
        else:
            result[value] = list(active)
    return result


def zero(final, symbols, live, end):
    keep = bytearray(end)
    dead = bytearray(end)
    for index, (address, size, _) in symbols.items():
        if index in live:
            keep[address:address + size] = b'\1' * size
        else:
            dead[address:address + size] = b'\1' * size
    zeroed = bytearray(final.data)
    for address, start, stop in final.data_segments():
        for offset in range(stop - start):
            at = address + offset
            if dead[at] and not keep[at]:
                zeroed[start + offset] = 0
    return bytes(zeroed), [at for at in range(end) if dead[at] and not keep[at]]


def report(folder, symbols, live, dead, literal_hits, final_bytes, zeroed):
    dead_set = set(dead)
    by_name = collections.Counter()
    for index, (address, size, name) in symbols.items():
        if index not in live:
            by_name[name] += sum(1 for at in range(address, address + size) if at in dead_set)
    sizes = {}
    for label, data in (('shipped', final_bytes), ('zeroed', zeroed)):
        sizes[label] = (len(subprocess.run(['brotli', '-q', '11', '-c'], input=data, stdout=subprocess.PIPE, check=True).stdout),
                        len(subprocess.run(['gzip', '-9', '-c'], input=data, stdout=subprocess.PIPE, check=True).stdout))
    lines = [f'dead data: {len(dead) / 1024:.1f} KiB raw of {len(symbols) - len(live)} symbols',
             f'wasm brotli {sizes["shipped"][0] / 1024:.1f} -> {sizes["zeroed"][0] / 1024:.1f} KiB '
             f'({(sizes["zeroed"][0] - sizes["shipped"][0]) / 1024:+.1f}), gzip {sizes["shipped"][1] / 1024:.1f} -> '
             f'{sizes["zeroed"][1] / 1024:.1f} KiB ({(sizes["zeroed"][1] - sizes["shipped"][1]) / 1024:+.1f})', '']
    lines += ['Dead though constants of the code fall inside them (literals of the code before the post-link optimization, '
              f'{WINDOW} bytes or more from every relocation):']
    lines += [f'{symbols[index][1]:8} {symbols[index][2]}: ' + ', '.join(f'{value} (+{value - symbols[index][0]:#x})' for value in values[:6])
              for index, values in sorted(literal_hits.items(), key=lambda item: -symbols[item[0]][1])]
    lines += ['', 'Dead bytes by symbol:']
    lines += [f'{size:8} {name}' for name, size in by_name.most_common() if size]
    with open(os.path.join(folder, 'dead.txt'), 'w') as file:
        file.write('\n'.join(lines) + '\n')
    return {'deadBytes': len(dead), 'deadSymbols': len(symbols) - len(live), 'brotliBefore': sizes['shipped'][0],
            'brotliAfter': sizes['zeroed'][0], 'gzipBefore': sizes['shipped'][1], 'gzipAfter': sizes['zeroed'][1]}


def smoke(project, shipped, output):
    folder = os.path.join(project, 'DeadData')
    copy = os.path.join(folder, 'Build-WebGL')
    shutil.rmtree(copy, ignore_errors=True)
    shutil.copytree(os.path.join(project, 'Build-WebGL'), copy)
    target = os.path.join(copy, 'Build', os.path.basename(shipped))
    with open(target, 'wb') as file:
        subprocess.run(['brotli', '-q', '11', '-c', output], stdout=file, check=True)
    run = subprocess.run(['node', os.path.join(TOOLS, 'smoke.mjs'), copy, os.path.join(folder, 'smoke.png')],
                         stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    print(run.stdout[-1500:])
    if run.returncode != 0:
        raise DeadDataError(f'the smoke run of the zeroed wasm failed ({run.returncode})')


if __name__ == '__main__':
    main()
