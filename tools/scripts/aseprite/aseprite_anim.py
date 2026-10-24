"""Multi-tag, multi-layer .aseprite files from PNG frames by the Aseprite CLI (Lua).

build(out, clips, size, layers=('Body',), duration_ms=83)
    clips: list of (tag, frames) in order. A frame is a PNG path (it goes to the first layer) or a dict
    {layer: PNG path}; a layer missing in a frame stays empty there. Every PNG is size (w, h). duration_ms is one
    number for every frame or a dict {tag: ms}. layers: bottom to top.

Every layer gets a stable uuid (aseprite_uuid.stamp): Unity keeps the sprite ids on reimport. The Unity importer names
a clip <file name><tag without '_'>: Gop.aseprite + Idle_Face -> GopIdleFace. A custom animation of the sprite catalog
takes a subset of the layers (the variant without the arms: the tag with the Body layer only).

Aseprite: ASEPRITE env or the Steam install. CLI: python -I aseprite_uuid.py <file or dir> to stamp existing files.
"""
import os, subprocess, tempfile, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aseprite_uuid

ASE = os.environ.get('ASEPRITE', os.path.expanduser('~/.steam/steam/steamapps/common/Aseprite/aseprite'))


def _lua_path(path):
    return os.path.abspath(path).replace('\\', '/')


def build(out, clips, size, layers=('Body',), duration_ms=83):
    w, h = size
    frames = []
    for tag, paths in clips:
        for p in paths:
            frames.append((tag, p if isinstance(p, dict) else {layers[0]: p}))
    if not frames:
        raise ValueError('no frames')
    for tag, frame in frames:
        unknown = set(frame) - set(layers)
        if unknown:
            raise ValueError(f'{tag}: layers {sorted(unknown)} are not in {list(layers)}')

    lua = [f'local spr=Sprite({w},{h},ColorMode.RGB)', f'local L={{spr.layers[1]}}; L[1].name="{layers[0]}"']
    for i, name in enumerate(layers[1:], 2):
        lua.append(f'L[{i}]=spr:newLayer(); L[{i}].name="{name}"')
    for _ in range(1, len(frames)):
        lua.append('spr:newEmptyFrame()')
    for i, (tag, frame) in enumerate(frames, 1):
        for name, p in frame.items():
            k = layers.index(name) + 1
            lua.append(f'do local img=Image{{fromFile="{_lua_path(p)}"}}; spr:newCel(L[{k}],spr.frames[{i}],img,Point(0,0)) end')
        ms = duration_ms[tag] if isinstance(duration_ms, dict) else duration_ms
        lua.append(f'spr.frames[{i}].duration={ms / 1000}')
    start = 1
    for tag, paths in clips:
        lua.append(f'do local t=spr:newTag({start},{start + len(paths) - 1}); t.name="{tag}" end')
        start += len(paths)
    lua.append(f'spr:saveAs("{_lua_path(out)}")')

    fd, script = tempfile.mkstemp(suffix='.lua')
    os.write(fd, '\n'.join(lua).encode())
    os.close(fd)
    if os.path.exists(out):
        os.remove(out)
    r = subprocess.run([ASE, '-b', '--script', script], capture_output=True, text=True)
    os.remove(script)
    errors = [l for l in (r.stdout + r.stderr).splitlines() if l.strip() and 'handle-pose' not in l]
    if errors:
        print('\n'.join(errors[:5]))
    if not os.path.exists(out):
        raise RuntimeError(f'{out} was not written')
    aseprite_uuid.stamp(out)
