#!/usr/bin/env python3
"""Writes the files of a WebGL .data file (UnityWebData1.0, already unpacked from brotli) one after another, each UnityFS
archive among them (data.unity3d: the scenes and Resources of the player, LZ4 blocks) unpacked, so that a text search
over the output sees the strings of its assets, e.g. the content catalog in Resources.

Usage: webdata.py <unpacked .data> <output>
Only the standard library: LZ4 blocks are decoded here.
"""
import struct
import sys

WEBDATA = b'UnityWebData1.0\0'
UNITYFS = b'UnityFS\0'
LZ4_KINDS = (2, 3)
BLOCKS_AT_END = 0x80
ALIGNED_INFO = 0x200


def lz4_block(src, size):
    dst = bytearray()
    i = 0
    n = len(src)
    while i < n:
        token = src[i]
        i += 1
        length = token >> 4
        if length == 15:
            while True:
                b = src[i]
                i += 1
                length += b
                if b != 255:
                    break
        dst += src[i:i + length]
        i += length
        if i >= n:
            break
        offset = src[i] | (src[i + 1] << 8)
        i += 2
        match = token & 15
        if match == 15:
            while True:
                b = src[i]
                i += 1
                match += b
                if b != 255:
                    break
        match += 4
        start = len(dst) - offset
        if offset >= match:
            dst += dst[start:start + match]
        else:
            for k in range(match):
                dst.append(dst[start + k])
    if len(dst) != size:
        raise ValueError(f'LZ4 block unpacked to {len(dst)} bytes, expected {size}')
    return bytes(dst)


def unpack(data, size, kind):
    if kind == 0:
        return data
    if kind in LZ4_KINDS:
        return lz4_block(data, size)
    raise ValueError(f'UnityFS compression {kind} is not supported')


def cstr(b, p):
    end = b.index(b'\0', p)
    return b[p:end], end + 1


# The data of every node of a UnityFS archive, concatenated. Unity 7000: a block record is 18 bytes and carries the
# offset of its block, the blocks start aligned to 16 bytes.
def unityfs(b):
    _, p = cstr(b, 0)
    p += 4
    _, p = cstr(b, p)
    _, p = cstr(b, p)
    _, info_packed, info_size, flags = struct.unpack_from('>QIII', b, p)
    p += 20
    if flags & ALIGNED_INFO:
        p = (p + 15) & ~15
    if flags & BLOCKS_AT_END:
        info = b[len(b) - info_packed:]
    else:
        info = b[p:p + info_packed]
        p += info_packed
    info = unpack(info, info_size, flags & 0x3f)
    p = (p + 15) & ~15

    q = 16
    count = struct.unpack_from('>I', info, q)[0]
    q += 4
    out = bytearray()
    for _ in range(count):
        size, packed, block_flags, offset = struct.unpack_from('>IIHQ', info, q)
        q += 18
        out += unpack(b[p + offset:p + offset + packed], size, block_flags & 0x3f)
    return bytes(out)


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    d = open(sys.argv[1], 'rb').read()
    if not d.startswith(WEBDATA):
        sys.exit(f'webdata: {sys.argv[1]} is no UnityWebData1.0 file')
    header = struct.unpack_from('<I', d, len(WEBDATA))[0]
    p = len(WEBDATA) + 4
    with open(sys.argv[2], 'wb') as out:
        while p < header:
            offset, size, name_length = struct.unpack_from('<III', d, p)
            p += 12
            name = d[p:p + name_length]
            p += name_length
            blob = d[offset:offset + size]
            out.write(name + b'\n')
            out.write(unityfs(blob) if blob.startswith(UNITYFS) else blob)


if __name__ == '__main__':
    main()
