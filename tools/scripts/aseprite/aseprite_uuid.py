"""Stable layer UUIDs in .aseprite files.

Unity's AsepriteImporter keeps sprite ids across reimports by matching layers by uuid. A file without layer UUIDs
gets uuid = string.GetHashCode(layer path), and on the CoreCLR editor that hash is different in every editor process:
the uuid in the .meta never matches again, every reimport makes new sprite ids and all sprite references are lost.
A uuid stored in the file is taken as is, so every layer gets one here: (hash of the layer path, 0, 0, 0), a pure
function of the path - a rebuilt file keeps its sprite ids, the build stays bit-reproducible. The hash is the one Mono
gave, so .meta files written by the old editor stay valid.

stamp(path) -> True if the file was changed. CLI: aseprite_uuid.py [--check] <file or dir>...
"""
import os, struct, sys

HEADER_SIZE = 128
FLAGS_OFFSET = 14
LAYERS_HAVE_UUID = 4
FRAME_MAGIC = 0xF1FA
LAYER_CHUNK = 0x2004
LAYER_TILEMAP = 2


def stable_hash(value):
    """Mono's string.GetHashCode as uint32 (UTF-16 code units)."""
    units = struct.unpack(f'<{len(value.encode("utf-16-le")) // 2}H', value.encode('utf-16-le'))
    mask = 0xFFFFFFFF
    hash1 = hash2 = 5381
    for i in range(0, len(units), 2):
        hash1 = (((hash1 << 5) + hash1) & mask) ^ units[i]
        if i + 1 >= len(units):
            break
        hash2 = (((hash2 << 5) + hash2) & mask) ^ units[i + 1]
    return (hash1 + hash2 * 1566083941) & mask


def layer_uuid(path):
    return struct.pack('<4I', stable_hash(path), 0, 0, 0)


def convert(data):
    """bytes of an .aseprite file -> bytes with the stable uuid in every layer chunk."""
    size, magic, frames = struct.unpack_from('<IHH', data, 0)
    if magic != 0xA5E0:
        raise ValueError('not an .aseprite file')

    flags, = struct.unpack_from('<I', data, FLAGS_OFFSET)
    has_uuid = flags & LAYERS_HAVE_UUID != 0
    out = bytearray(data[:HEADER_SIZE])
    struct.pack_into('<I', out, FLAGS_OFFSET, flags | LAYERS_HAVE_UUID)

    parents = {}    # child level -> layer name
    seen = set()
    pos = HEADER_SIZE

    for _ in range(frames):
        frame_size, frame_magic, old_chunks, duration, _, new_chunks = struct.unpack_from('<IHHH2sI', data, pos)
        if frame_magic != FRAME_MAGIC:
            raise ValueError('bad frame header')

        end = pos + frame_size
        frame = bytearray(data[pos:pos + 16])
        pos += 16

        while pos < end:
            chunk_size, chunk_type = struct.unpack_from('<IH', data, pos)
            chunk = data[pos:pos + chunk_size]
            pos += chunk_size

            if chunk_type == LAYER_CHUNK:
                chunk = stamp_layer(chunk, has_uuid, parents, seen)

            frame += chunk

        struct.pack_into('<I', frame, 0, len(frame))
        out += frame

    out += data[pos:]
    struct.pack_into('<I', out, 0, len(out))
    return bytes(out)


def stamp_layer(chunk, has_uuid, parents, seen):
    layer_type, level = struct.unpack_from('<HH', chunk, 8)
    name_size, = struct.unpack_from('<H', chunk, 22)
    name = chunk[24:24 + name_size].decode('utf-8')
    tail = 24 + name_size + (4 if layer_type == LAYER_TILEMAP else 0)

    if len(chunk) != tail + (16 if has_uuid else 0):
        raise ValueError(f'unexpected layer chunk size of "{name}"')

    parents[level] = name
    path = '/'.join(parents[i] for i in range(level + 1))

    # The importer renames layers with the same path, such files are not made by our tools.
    if path in seen:
        raise ValueError(f'two layers "{path}"')
    seen.add(path)

    body = chunk[6:tail] + layer_uuid(path)
    return struct.pack('<IH', len(body) + 6, LAYER_CHUNK) + body


def stamp(path, check=False):
    with open(path, 'rb') as f:
        data = f.read()

    result = convert(data)
    if result == data:
        return False

    if check == False:
        with open(path, 'wb') as f:
            f.write(result)
    return True


def main(args):
    check = '--check' in args
    files = []
    for arg in args:
        if arg == '--check':
            continue
        if os.path.isdir(arg):
            for root, _, names in os.walk(arg):
                files += [os.path.join(root, n) for n in names if n.endswith(('.aseprite', '.ase'))]
        else:
            files.append(arg)

    changed = [f for f in sorted(files) if stamp(f, check)]
    for f in changed:
        print(('needs uuid: ' if check else 'stamped: ') + f)
    print(f'{len(changed)} of {len(files)} files {"need a stamp" if check else "stamped"}')
    return 1 if check and changed else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
