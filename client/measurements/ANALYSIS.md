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

## 7000.0.0a20 and the size switches (gzip)

Measured 2026-11-19. The sizes here are gzip (`gzip -9` of the unpacked files of `Build/`, the CDN of a game serves
gzip), KiB; RESULTS.md stays brotli.

- **a20 against a16**: base 7,211 against 7,134 (+77), aggressive 5,731 against 5,632 (+99), almost all in the wasm
  code (`CoreModule` +20, `EntitiesModule` +25 KiB raw). The built-in uGUI of a20 (new code under 7.0.0, measured with
  the patches of the fork) adds 6 KiB raw of assemblies.
- **Where the code of aggressive goes** (10.0 MB raw before the submodule stripping, by the name section and the
  `MethodMap.tsv` of IL2CPP): engine 4.3 MB (serialization 0.9 with `GenerateTypeTreeTransfer` 0.3, rendering 0.6,
  graphics device 0.35), `System.Private.CoreLib` 2.0 MB, the ICU of the BCL 1.5 MB, `EntitiesModule` +
  `ManagedKernelModule` 0.5 MB, the IL2CPP runtime 0.3 MB. The ICU of the BCL is not the `ICU` submodule of Unity (that
  one is the ICU of the engine, `icu_73::`), so no list strips it.
- **The column "wasm before submodule stripping"** includes the name section (8.9 MB raw of 21 MB): the stripping
  itself removed 1.1 MB raw of code in aggressive, the rest of the difference is the names.

| aggressive on a20 | Download | wasm | data | Saved |
|---|---:|---:|---:|---:|
| aggressive | 5,731 | 3,945 | 1,704 | |
| incremental GC off (`gcIncremental` 0) | 5,731 | 3,945 | 1,704 | 0: the wasm is the same byte for byte |
| `aggressive-novector` | 5,648 | 3,896 | 1,669 | -83 (-1.4%) |
| `aggressive-size` (Optimize Size) | 5,296 | 3,658 | 1,556 | -435 (-7.6%) |
| `aggressive-invariant` | 4,682 | 3,340 | 1,260 | -1,049 (-18.3%) |
| `aggressive-lean` (invariant + resource keys + Optimize Size) | 4,179 | 3,053 | 1,044 | -1,552 (-27.1%) |

- **The invariant globalization** needs four pieces, each found in the decompiled build programs of a20: no player
  setting reaches `DotnetRuntimeFeatures.InvariantGlobalization` of the IL2CPP build (the player build program does
  not copy it from the IL2CPP arguments), so ICU is still compiled and `icudt.dat` still packed.
  - the feature switch of UnityLinker alone does not do it: `GlobalizationMode.Settings` reads
    `System.Globalization.Invariant` from the AppContext at run time and the player stops with `Unable to load
    required ICU Globalization data`;
  - `--compiler-flags=-DIL2CPP_SYSTEM_GLOBALIZATION_INVARIANT=1` in the IL2CPP arguments reaches every native program,
    libil2cpp too (`AppContext::SetupAppContextSwitches` sets the switch, `IcuData::LoadIcuData` loads nothing). A
    define through `--additional-defines` reaches only the generated code;
  - with the BCL invariant the ICU code is still reachable (the interop of the BCL keeps both branches), so the
    submodule `BCL Globalization ICU` strips it: -1.5 MB raw of wasm. A `--trace-missing` run of `aggressive-lean` calls
    none of it;
  - `icudt.dat` (1.5 MB raw) is taken out of the `.data` file after the build.
  For a game: `CultureInfo.CurrentCulture` is the invariant culture, `new CultureInfo("en-US")` throws
  (`CultureNotFoundException`, the predefined cultures only), formatting and parsing are invariant, casing stays full
  Unicode. In the games only the `SimpleFileBrowser` plugin of storytale-workshop makes a culture by name.
- **Optimize Size** saves more than on a10 (-308 against -119 KiB brotli): the generics of the .NET 10 BCL.
- **`UseSystemResourceKeys`** (in lean): about -50 KiB brotli, the resources of the assemblies; exception messages become keys.
- **The vector substitutions** remove 189 KiB raw of wasm (the vectorized branches of the span helpers), -83 gzip. They
  cost no speed: libil2cpp answers `false` to `IsHardwareAccelerated` (`intrinsics/.../Vector128.h`), so the player ran
  the scalar branches already; IL2CPP compiled the vectorized ones because the method tables reference every method
  the linker keeps.
- **On a20 aggressive calls a stripped function**: `InstancingBatcher::~InstancingBatcher()` of `GPU Instancing`, once
  in the sprite part (seen with `--trace-missing`). With `Ignore` the destructor does not run.

## aggressive-lean of a20 below 2.9 MB brotli

Measured 2026-11-22; brotli KiB as RESULTS.md (gzip at the end). Each row adds to the one above.

| aggressive-lean on a20 | Download | Saved |
|---|---:|---:|
| lean of 2026-11-19 (invariant + resource keys + Optimize Size) | 3,195 | |
| the trimmed MemoryPack fork | 3,161 | -34 |
| `unused.txt`: Safe Binary Read, Graphics State Collection, Platform Audio, Content Load Backend, Text From Binary Renderer, BCL Event Source, BCL JavaScript Interop | 3,084 | -77 |
| `zeroDeadData` | 2,957 | -127 |
| the vector substitutions | 2,918 | -39 |
| `reoptimize` | 2,899 | -19 |
| `unused.txt`: Content Load Manager, BCL Resources | 2,874 | -25 |

In gzip the download went from 4,179 to 3,795 (wasm 3,053 to 2,710, data 1,044 to 1,004).

- **MemoryPack**: the fork keeps only the formatters the protocols of the games use (primitives, string, Guid, enums,
  arrays, `List`, `Dictionary`, `IReadOnlyList`, `IReadOnlyDictionary`, nullables, `DateTime` / `TimeSpan`, Unity
  structs, unmanaged structs): the other collections, the array-likes (`Memory`, `ArraySegment`, sequences, pools),
  `decimal` and `DateTimeOffset` went, and the BCL code only they reached.
- **`unused.txt`**: functions of at least 200 bytes that neither scene (`aggressive-lean`, `aggressive-lean-assetbundles`)
  calls, in groups whose reason holds for a game (`tools/unused_definition.py`, the description of each group in the
  definitions). The largest: the reading of serialized data with another type tree (127 KiB raw, the bundles of
  `ContentBuild` have no type trees), the GraphicsStateCollection API (83), EventSource (67: `IsSupported` is false
  for IL2CPP, but the type keeps all its methods), the content files of `Unity.Loading` (35 + 19: no game uses the
  API), the lookup of the resource strings of the BCL (24: with `UseSystemResourceKeys` the linker sets
  `SR.s_usingResourceKeys` to true). `BCL Resources` only goes with `UseSystemResourceKeys`.
- **The dead data of the wasm** (`tools/dead_data.py`): 411 KiB raw of 1,379 the code left after the stripping no
  longer reaches, zeroed: the tables of ICU (the property tries, the names of the properties, the normalization data:
  ~260 KiB), the tables of the stripped texture decoders (BC6H, BC7, ASTC), the strings of the stripped code. The wasm
  alone: -100 KiB brotli, -116 gzip at the same compression. The other ~29 KiB of the row is the window of brotli: the
  file is recompressed with `brotli -q 11` (window 24 bits), Unity keeps 22. The relocations of a link with
  `--emit-relocs` tell the addresses from the numbers; only a few tables are decided by the literal rule (`0xF0000`,
  `0x10FFFF`, `999999` in the middle of the tries of ICU, `429496` in a table of BC6H), listed first in `dead.txt`.
  Packing the segments afterwards (`--memory-packing`) gives a smaller raw file but a larger brotli one.
- **`reoptimize`**: `--gufa-optimizing -Oz` on the stripped wasm follows through the whole program the defaults the
  stubs of the removed functions return. A second `-Oz` alone gives about the same; `--converge` nothing more. Without
  `--optimize-stack-ir` as the last pass binaryen writes the same module 38 KB larger in brotli.
- **Tried and dropped**:
  - ILLink substitutions of `SR.InternalGetResourceString`, of the editor connection of `PlayerConnection` and of the
    static constructor of `BurstCompilerOptions`: -5 KiB, the same BCL code stays through other paths;
  - `--ignore-descriptors` (the descriptor of CoreLib, written for the Mono runtime): -44 KiB, but the player hangs at
    the start: libil2cpp needs some of the types it keeps (`ThreadAbortException`, `TargetException`, `Pointer`, ...);
  - `--used-attrs-only`: IL2CPP fails, the `UnsafeAccessor` of `NoAllocHelpers` loses its attribute;
  - a link attributes file against the `DynamicallyAccessedMembers` of `EventSource`: no effect;
  - `GenerateTypeTreeTransfer` (-55 KiB in a simulation): the player builds the type trees of the loaded types when
    it reads an AssetBundle, even one without type trees.
- **Left** (by size): a reduced copy of the descriptor of CoreLib with what libil2cpp names (up to -44); the objects of
  `unity default resources` a game never references (13 primitive meshes 47 KiB brotli, the legacy font 28, textures
  20), by the references of the game; the code of the built-in render pipeline (`Camera::DefaultPerformRenderFunction`,
  `DoForwardShaderRenderLoop`, ...: every game is on URP), with the coverage of a game, since URP shares the shadow and
  culling code.

### 7000.0.0a21 and stripping tool 1.4.1 (2026-11-24)

| Build | brotli | gzip | wasm br | data br |
|---|---:|---:|---:|---:|
| a20, tool 1.4.0 | 2,874 | 3,795 | 1,974 | 811 |
| a20, tool 1.4.1 | 2,872 | 3,793 | 1,973 | 809 |
| a21, tool 1.4.1 | 2,899 | 3,820 | 1,990 | 820 |

- The tool changes nothing in size: 1.4.1 fixes the editor windows, the MethodMap copy and the obsolete Wasm settings
  of Unity 7, the passes of wasm-opt are the same.
- a21 is +25 KiB brotli of the engine itself: the managed `UnityEngine.EntitiesModule` +29 KiB (so the metadata +8 KiB
  brotli), the code section +36 KB raw, the data section +29 KB raw (fewer dead bytes: 386 KiB instead of 411).
- The definitions written from a20 hold: every function of the groups of `engine_unused.json` and `bcl_unused.json`
  is in a21 (one of the 166 of Safe Binary Read is gone), the scene runs (smoke ready).

## Next

- The same method as `unused.txt` on a game (pixel-swarm): its own coverage, then the groups that hold for it.
- `lean` on `base` and on a game (pixel-swarm), with its own start and scenes checked.
- A variant with Addressables and the client core of the framework (the container, scopes), which the scene does
  not have, so their cost per version is measured too.
- Each new Unity release: `tools/measure.py <version>`, then this file.
