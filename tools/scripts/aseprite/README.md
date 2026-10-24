# Aseprite scripts

Python 3 without packages, Aseprite CLI (`ASEPRITE` env or the Steam install).

- `aseprite_anim.py` - `build(out, clips, size, layers, duration_ms)`: a multi-tag, multi-layer `.aseprite` from PNG
  frames, the layers stamped with stable UUIDs.
- `aseprite_uuid.py [--check] <file or dir>...` - stable layer UUIDs in existing files. Without them the Unity
  importer derives the layer id from `string.GetHashCode`, which differs in every CoreCLR editor process: every
  reimport makes new sprite ids and the references to the sprites are lost.

A file for the sprite catalog: `spriteCatalog` in the userData of its `.meta` (kind `MultipleAnimations`, custom
animations take a subset of the layers), see `client/core/Editor/Catalogues/Sprites`.
