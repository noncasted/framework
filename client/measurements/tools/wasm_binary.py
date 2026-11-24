"""Reading of a wasm binary for the size tools: sections, the function bodies with their instructions, data segments,
the linking and relocation sections of a wasm-ld output linked with --emit-relocs, the function names."""

# Relocation types of the wasm object format (llvm/BinaryFormat/WasmRelocs.def) that carry an addend.
ADDEND_TYPES = {3, 4, 5, 8, 9, 11, 14, 15, 16, 17, 21, 22, 23, 25}
MEMORY_ADDRESS_TYPES = {3, 4, 5, 11, 14, 15, 16, 17, 21, 23, 25}


def uleb(data, position):
    result = shift = 0
    while True:
        byte = data[position]
        position += 1
        result |= (byte & 0x7F) << shift
        shift += 7
        if byte < 0x80:
            return result, position


def sleb(data, position):
    result = shift = 0
    while True:
        byte = data[position]
        position += 1
        result |= (byte & 0x7F) << shift
        shift += 7
        if byte < 0x80:
            if byte & 0x40:
                result -= 1 << shift
            return result, position


def name(data, position):
    length, position = uleb(data, position)
    return data[position:position + length].decode('utf-8', 'replace'), position + length


class Module:
    def __init__(self, data):
        if data[:4] != b'\0asm':
            raise ValueError('not a wasm binary')
        self.data = data
        self.sections = []  # (id, custom name or None, payload start, payload end)
        position = 8
        while position < len(data):
            section, position = data[position], position + 1
            size, position = uleb(data, position)
            custom = name(data, position)[0] if section == 0 else None
            self.sections.append((section, custom, position, position + size))
            position += size

    def section(self, section, custom=None):
        found = [entry for entry in self.sections if entry[0] == section and entry[1] == custom]
        if len(found) != 1:
            raise ValueError(f'{len(found)} sections {section} {custom}')
        return found[0]

    def has(self, section, custom=None):
        return any(entry[0] == section and entry[1] == custom for entry in self.sections)

    def custom_payload(self, custom):
        _, _, start, end = self.section(0, custom)
        return name(self.data, start)[1], end

    def imported_functions(self):
        if not self.has(2):
            return 0
        data = self.data
        _, _, position, _ = self.section(2)
        count, position = uleb(data, position)
        functions = 0
        for _ in range(count):
            _, position = name(data, position)
            _, position = name(data, position)
            kind, position = data[position], position + 1
            if kind == 0:
                functions += 1
                _, position = uleb(data, position)
            elif kind == 1:
                position += 1
                flags, position = uleb(data, position)
                _, position = uleb(data, position)
                if flags & 1:
                    _, position = uleb(data, position)
            elif kind == 2:
                flags, position = uleb(data, position)
                _, position = uleb(data, position)
                if flags & 1:
                    _, position = uleb(data, position)
            elif kind == 3:
                position += 1
                _, position = uleb(data, position)
                position += 1
            elif kind == 4:
                position += 1
                _, position = uleb(data, position)
            else:
                raise ValueError(f'import kind {kind}')
        return functions

    def bodies(self):
        """(function index, start, end) of every body: start is the first byte after its size (the locals)."""
        data = self.data
        _, _, position, _ = self.section(10)
        base = self.imported_functions()
        count, position = uleb(data, position)
        result = []
        for index in range(count):
            size, position = uleb(data, position)
            result.append((base + index, position, position + size))
            position += size
        return result

    def data_segments(self):
        """(address, bytes start, bytes end) of every active segment with an i32.const offset."""
        data = self.data
        _, _, position, _ = self.section(11)
        count, position = uleb(data, position)
        result = []
        for _ in range(count):
            flags, position = uleb(data, position)
            if flags == 1:
                size, position = uleb(data, position)
                position += size
                continue
            if flags == 2:
                _, position = uleb(data, position)
            if data[position] != 0x41:
                raise ValueError('a data segment offset is not i32.const')
            address, position = sleb(data, position + 1)
            if data[position] != 0x0B:
                raise ValueError('a data segment offset is not a single i32.const')
            size, position = uleb(data, position + 1)
            result.append((address, position, position + size))
            position += size
        return result

    def function_names(self):
        if not self.has(0, 'name'):
            return {}
        data = self.data
        position, end = self.custom_payload('name')
        names = {}
        while position < end:
            kind, position = data[position], position + 1
            size, position = uleb(data, position)
            if kind == 1:
                count, inner = uleb(data, position)
                for _ in range(count):
                    index, inner = uleb(data, inner)
                    names[index], inner = name(data, inner)
            position += size
        return names

    def global_constants(self):
        """The i32.const values of the global initializers."""
        if not self.has(6):
            return []
        data = self.data
        _, _, position, _ = self.section(6)
        count, position = uleb(data, position)
        values = []
        for _ in range(count):
            position += 2  # value type, mutability
            while data[position] != 0x0B:
                opcode, position = data[position], position + 1
                if opcode == 0x41:
                    value, position = sleb(data, position)
                    values.append(value)
                elif opcode == 0x42:
                    _, position = sleb(data, position)
                elif opcode == 0x23:
                    _, position = uleb(data, position)
                else:
                    raise ValueError(f'global initializer opcode {opcode:#x}')
            position += 1
        return values

    def relocations(self, custom):
        """(type, offset in the payload of the target section, symbol index, addend) of a reloc.* section."""
        data = self.data
        position, _ = self.custom_payload(custom)
        _, position = uleb(data, position)
        count, position = uleb(data, position)
        result = []
        for _ in range(count):
            kind, position = uleb(data, position)
            offset, position = uleb(data, position)
            index, position = uleb(data, position)
            addend = 0
            if kind in ADDEND_TYPES:
                addend, position = sleb(data, position)
            result.append((kind, offset, index, addend))
        return result

    def data_symbols(self):
        """Symbol index -> (name, segment index, offset in the segment, size) of every defined data symbol of the
        symbol table of the linking section; other symbols are absent."""
        data = self.data
        position, end = self.custom_payload('linking')
        _, position = uleb(data, position)
        symbols = {}
        while position < end:
            kind, position = data[position], position + 1
            size, position = uleb(data, position)
            next_position = position + size
            if kind == 8:
                count, position = uleb(data, position)
                for index in range(count):
                    symbol_kind, position = data[position], position + 1
                    flags, position = uleb(data, position)
                    if symbol_kind in (0, 2, 4, 5):  # function, global, tag, table
                        _, position = uleb(data, position)
                        if not flags & 0x10 or flags & 0x40:  # defined, or an explicit name
                            _, position = name(data, position)
                    elif symbol_kind == 1:
                        symbol, position = name(data, position)
                        if not flags & 0x10:
                            segment, position = uleb(data, position)
                            offset, position = uleb(data, position)
                            length, position = uleb(data, position)
                            symbols[index] = (symbol, segment, offset, length)
                    elif symbol_kind == 3:
                        _, position = uleb(data, position)
                    else:
                        raise ValueError(f'symbol kind {symbol_kind}')
            position = next_position
        return symbols


def instructions(data, start, end, on_constant):
    """Walks the instructions of a body (start: its locals): on_constant(value, position of its LEB) for an i32.const
    and for the offset of a memory access."""
    position = start
    count, position = uleb(data, position)
    for _ in range(count):
        _, position = uleb(data, position)
        position = value_type(data, position)
    while position < end:
        opcode = data[position]
        position += 1
        if opcode == 0x41:
            value, next_position = sleb(data, position)
            on_constant(value & 0xFFFFFFFF, position)
            position = next_position
        elif 0x28 <= opcode <= 0x3E:
            position = memarg(data, position, on_constant)
        elif opcode in (0x20, 0x21, 0x22, 0x23, 0x24, 0x0C, 0x0D, 0x07, 0x08, 0x09, 0x18, 0x25, 0x26, 0xD5, 0xD6, 0x10, 0x12, 0xD2):
            _, position = uleb(data, position)
        elif opcode in (0x02, 0x03, 0x04, 0x06):
            position = block_type(data, position)
        elif opcode in (0x11, 0x13):
            _, position = uleb(data, position)
            _, position = uleb(data, position)
        elif opcode in (0x14, 0x15):
            _, position = uleb(data, position)
        elif opcode == 0x0E:
            targets, position = uleb(data, position)
            for _ in range(targets + 1):
                _, position = uleb(data, position)
        elif opcode == 0x42:
            _, position = sleb(data, position)
        elif opcode == 0x43:
            position += 4
        elif opcode == 0x44:
            position += 8
        elif opcode == 0x1C:
            types, position = uleb(data, position)
            for _ in range(types):
                position = value_type(data, position)
        elif opcode in (0x3F, 0x40):
            _, position = uleb(data, position)
        elif opcode == 0xD0:
            position = heap_type(data, position)
        elif opcode == 0x1F:
            position = block_type(data, position)
            clauses, position = uleb(data, position)
            for _ in range(clauses):
                clause, position = data[position], position + 1
                if clause in (0, 1):
                    _, position = uleb(data, position)
                _, position = uleb(data, position)
        elif opcode == 0xFC:
            position = prefixed_misc(data, position)
        elif opcode == 0xFD:
            position = prefixed_simd(data, position, on_constant)
        elif opcode == 0xFE:
            sub, position = uleb(data, position)
            if sub == 0x03:
                position += 1
            else:
                position = memarg(data, position, on_constant)
        elif opcode in (0x00, 0x01, 0x05, 0x0A, 0x0B, 0x0F, 0x19, 0x1A, 0x1B, 0xD1, 0xD3, 0xD4) or 0x45 <= opcode <= 0xC4:
            pass
        else:
            raise ValueError(f'opcode {opcode:#x} at {position - 1}')
    if position != end:
        raise ValueError(f'a body decoded past its end: {position} > {end}')


def memarg(data, position, on_constant):
    align, position = uleb(data, position)
    if align & 0x40:
        _, position = uleb(data, position)
    offset, next_position = uleb(data, position)
    on_constant(offset, position)
    return next_position


def value_type(data, position):
    kind = data[position]
    if kind in (0x63, 0x64):
        return heap_type(data, position + 1)
    return position + 1


def heap_type(data, position):
    _, position = sleb(data, position)
    return position


def block_type(data, position):
    kind = data[position]
    if kind == 0x40 or kind in (0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x70, 0x6F, 0x69):
        return position + 1
    if kind in (0x63, 0x64):
        return heap_type(data, position + 1)
    _, position = sleb(data, position)
    return position


def prefixed_misc(data, position):
    sub, position = uleb(data, position)
    immediates = {8: 2, 9: 1, 10: 2, 11: 1, 12: 2, 13: 1, 14: 2, 15: 1, 16: 1, 17: 1}.get(sub, 0)
    for _ in range(immediates):
        _, position = uleb(data, position)
    return position


def prefixed_simd(data, position, on_constant):
    sub, position = uleb(data, position)
    if sub <= 11 or sub in (92, 93):
        return memarg(data, position, on_constant)
    if sub in (12, 13):
        return position + 16
    if 21 <= sub <= 34:
        return position + 1
    if 84 <= sub <= 91:
        return memarg(data, position, on_constant) + 1
    return position
