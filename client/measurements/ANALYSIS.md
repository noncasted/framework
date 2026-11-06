# WebGL build size: analysis

Written by hand from [RESULTS.md](RESULTS.md) (measured 2026-11-06); the numbers there are generated, the conclusions
here are not. Download = wasm + data + framework.js + loader, brotli as served, KiB.

## Versions

| Variant | 6000.6.8f1 | 6000.7.0b8 | 7000.0.0a10 | 7000.0.0a11 | 7000.0.0a12 | 7000.0.0a16 (.NET 10 BCL) |
|---|---:|---:|---:|---:|---:|---:|
| base | 4,013 | 3,771 | **3,624** | 3,875 | 3,972 | **5,362** |
| aggressive | 2,851 | 2,598 | **2,614** | 2,866 | 2,909 | **4,278** |

The brotli of the whole data file moves by about ±20 KiB with any small change of the
scene (16 bytes more in `data.unity3d` gave 16 KiB more), so differences below that are noise.

- **6.7 and Unity 7 before .NET 10 are lighter than 6.6**: the wasm code is 7.4-7.8 MB raw against 10.5 MB; the
  managed assemblies are about the same (`mscorlib` 1.2 MB everywhere). 6.7 lands next to 7000.0.0a10.
- **7000.0.0a11 adds `UnityEngine.EntitiesModule`** (20 -> 307 KiB raw after the linker) and a larger
  `ManagedKernelModule` (51 -> 137 KiB): +250 KiB download in both variants, the scene uses none of it. It is an engine
  module (no `com.unity.modules.*` package to remove, no submodule of the stripping tool).
- **7000.0.0a12**: `UnityEngine.CoreModule` 430 -> 508 KiB raw, +40-100 KiB download.
- **The .NET 10 BCL of a16 adds about 1.4 MB** against a12 with the same scene and stripping:
  - `System.Private.CoreLib` after the linker is 1.8 MB against 1.2 MB of `mscorlib`; all assemblies 4.4 MB against 3.8 MB;
  - the IL2CPP metadata in the data file: 831 against 624 KiB br (3.1 against 2.4 MB raw);
  - new in the data file: the ICU data of the BCL `Il2CppData/Icu/icudt.dat`, 322 KiB br (1.5 MB raw), and the
    resources of the assemblies (exception messages), 59 KiB br;
  - the wasm before the submodule stripping is 6.6 MB br against 5.2 MB; the stripping removes more (2.9 against
    2.3 MB), the rest stays.

## Variants

| | 6000.6.8f1 | 6000.7.0b8 | 7000.0.0a10 | 7000.0.0a11 | 7000.0.0a12 | 7000.0.0a16 |
|---|---:|---:|---:|---:|---:|---:|
| saved by `aggressive` | -1,162 | -1,173 | -1,010 | -1,009 | -1,064 | -1,084 |

No canvas, uGUI, TextMeshPro and physics (2D and 3D), the packages the scene does not use removed from the manifest
and the text, tilemap, physics and BCL submodules stripped save 1.0-1.2 MB in every version. Nothing else pulls uGUI in (only the built-in `com.unity.textmeshpro` depends on
it, and the manifest does not list it); `UniTask.TextMeshPro` compiles to an empty assembly.

## What else can be stripped (submodule profiling)

`--profile` builds without the submodule stripping, instruments the build and lists the submodules the scene called
(`profiles/`):

- `aggressive` on a10 and a16: no candidates left, nothing called is stripped. The submodules are exhausted for this
  scene; what remains is the code it calls, the engine modules without submodules (EntitiesModule) and the BCL.
- Two submodules of the first aggressive list are called by the sprites: `Mesh Skinning` and
  `2D Rendering Sprite Atlas`. The page stayed alive only through `Ignore`, so they are kept.
- `base` on a10 (the list of pixel-swarm against this scene): never called and not stripped are
  `2D Rendering Sprite Tiling`, `Clipper`, `GPU Instancing`, `System.Security Core`, `System.TimeZoneInfo`,
  `Texture Decompression Crunch`, `Tilemap Core`, `Tilemap Script Bindings`, `libtess2`. These are candidates for the
  games, each checked on the game itself: the scene has no tilemaps, crunched textures or sprite shapes.
- The submodule definitions of the BCL (`System.*`) target the old mscorlib: on a16 the profiler reports no `System.*`
  submodule (106 profiled submodules against 114 on a10), so the BCL of .NET 10 cannot be stripped through them.

## What broke and what it says about the stripping on .NET 10

- **The group `Advanced Text Generator Support` contains `ICU`** (`advanced_text_generator.json` of the stripping
  tool: HarfBuzz, ICU, TextCoreTextEngine). Stripping the group strips ICU even with `ICU` kept, and on a16 the .NET
  10 BCL calls ICU at the start of the player (the AppContext setup of IL2CPP with `icudt.dat`, then
  `ulocdata_getCLDRVersion`): the player stops. `aggressive` strips `TextCoreTextEngine` instead of the group and
  keeps `ICU` from a16. A game on a16 that strips ICU, or the group, has to check its own start: a stopped player logs
  `Unable to load required ICU Globalization data` or an ICU call that failed.
- The libil2cpp of a16 has the compile time switch `IL2CPP_SYSTEM_GLOBALIZATION_INVARIANT` (`vm/AppContext.cpp`):
  the BCL in the invariant mode, no ICU. No player setting for it was found; if Unity exposes it (or it can be set
  through the IL2CPP arguments), ICU and `icudt.dat` (~0.3 MB br, more with the ICU code) are the first thing to cut.
- A TextMeshPro font asset that keeps its source font makes the player load it through Freetype and the font
  engine, which the list strips: the player hangs at the start without an error. The fonts of pixel-swarm are static
  without a source font; the built-in TMP Settings of the uGUI fork name the dynamic LiberationSans SDF, so a game
  that has no TMP Settings of its own ships it.
- The batchmode build runs with `-nographics`: with a graphics device the editor opened the TMP importer window
  (`TMP_Settings` found no settings asset while the project was assembled). The wasm is the same either way.

## Next

- An `invariant` variant for a16 when the switch is reachable: the size without ICU.
- `UnityEngine.EntitiesModule` (+250 KiB from a11): a submodule definition of our own for its code, if the stripping
  tool can cut an engine module that the scene never calls.
- A variant with Addressables and the client core of the framework (the container, scopes), which the scene does
  not have, so their cost per version is measured too.
- Each new Unity release: `tools/measure.py <version>`, then this file.
