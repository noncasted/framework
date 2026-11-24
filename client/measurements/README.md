# WebGL build size measurements

How the size of a WebGL build changes between Unity versions with the same stripping and the same web profile.
One command per Unity version: it generates a project per stripping variant, builds each in batchmode, measures
the files, runs the build in headless Chromium and adds the numbers to [RESULTS.md](RESULTS.md).

```bash
client/measurements/tools/measure.py 7000.0.0a16              # every variant of variants/
client/measurements/tools/measure.py 7000.0.0a16 aggressive   # one variant
client/measurements/tools/measure.py 7000.0.0a16 --skip-build # measure the last build again
client/measurements/tools/report.py                           # rebuild RESULTS.md from results/
```

The editor is `/tools/unity/<version>/Editor/Unity` (`UNITY_EDITORS` overrides `/tools/unity`) with WebGL support.
Builds take the batchmode slots of `deploy-client` (`UNITY_BUILD_SLOTS`, 2 by default), so two runs of different
versions may go in parallel. The first build of a project imports it from nothing (several minutes); `Library` is kept
for the next runs. Needs `brotli`, `node` >= 22 and Chromium (the stand of `tools/scripts/web-bench`).

## Layout

| Path | What |
|---|---|
| `template/` | the project every variant starts from: `Assets/Measurement` (the scene content, the editor build) and `Packages/manifest.json` |
| `template/Assets/Measurement/Profile/Web.asset` | the web build profile of pixel-swarm (`website.asset`): only the name, the page template (`APPLICATION:Minimal`), the defines of Odin and the preloaded assets differ |
| `parts/<part>/` | content a variant adds over the template (`ui`: a camera canvas with a layout, an image, TextMeshPro and a button; `physics`: 2D bodies; `addressables`: a group of bundles with a prefab and a scene, loaded from StreamingAssets; `assetbundles`: the same as three AssetBundles of the engine, built without type trees as `ContentBuild` builds the bundles of a game) |
| `variants/<variant>.json` | `parts`, extra `packages` (`framework:<path>` is a folder of this repository), the `strip` lists, the `overrides` of some versions; optional: `profile` (player settings of the web profile: the WebGL entry of a map per platform or a plain value), `linkerArgs` (arguments of UnityLinker, through the diagnostic switch `VMUnityLinkerAdditionalArgs`: the build log must show them), `dropData` (files taken out of the `.data` container after the build, recompressed with `brotli -q 11`), `zeroDeadData` (the data of the wasm no code reaches after the stripping zeroed, `tools/dead_data.py`), `reoptimize` (the stripped wasm optimized once more: `--gufa-optimizing -Oz`). The steps after the build recompress with `brotli -q 11`, whose window (24 bits) is larger than the one of Unity (22): the wasm alone is ~1.4% smaller for that |
| `template/ProjectSettings/` | the project settings of pixel-swarm that change the player (graphics: the built-in shaders off, quality, physics, audio, time, tags, input); the pipeline is set by the build |
| `stripping/*.txt` | submodule names, one a line; `?` marks a name not every version has (skipped there and listed in the results) |
| `profiles/<version>/<variant>.json` | the submodules a profiling run called and the candidates for stripping (`--profile`) |
| `results/<version>/<variant>.json` | one measurement: the files, the wasm sections, the wasm before the submodule stripping, the assemblies after the linker, the smoke run |
| `RESULTS.md` | generated tables: sizes, savings of the variants, changes against the first version, the assemblies |
| `projects/<version>-<variant>/` | generated projects (git-ignored): open one in its editor to look inside; `build.log`, `smoke.png`, `Build-WebGL/` |

## Variants

- `base`: the stripping of pixel-swarm (`client/Assets/Settings/DefaultSubmoduleStrippingSettings.asset`), the list a
  game runs with. Content: sprites, 2D physics, audio, uGUI and TextMeshPro with a static SDF font.
- `aggressive`: `base` without the canvas, uGUI and TextMeshPro, and `stripping/aggressive.txt` on top (text rendering,
  IMGUI, the rest of Freetype, tilemaps, mesh skinning, ...) and `stripping/physics2d.txt`.
- `aggressive-size`: `aggressive` with the IL2CPP code generation Optimize Size (Faster (smaller) builds).
- `aggressive-invariant`: `aggressive` with the BCL in the invariant globalization mode, which no player setting
  exposes: the feature switch of UnityLinker (`--dotnet-runtime-feature=InvariantGlobalization`), the define
  `IL2CPP_SYSTEM_GLOBALIZATION_INVARIANT` of libil2cpp (`--compiler-flags=-D...` in the IL2CPP arguments of the profile:
  the switch in the AppContext at the start), the ICU of the BCL stripped (`stripping/invariant.txt`, the definition
  `bcl_globalization.json` of the stripping tool fork, `tools/bcl_icu_definition.py <version> <project>` writes it again
  for another ICU) and `Il2CppData/Icu/icudt.dat` dropped from the `.data` file (the build packs it anyway).
- `aggressive-lean`: every switch that changed the size: `aggressive-invariant` + `UseSystemResourceKeys` (exception
  messages of the BCL are their resource keys) + Optimize Size + the vector substitutions (as `aggressive-novector`) +
  `stripping/unused.txt` (code no measured scene calls: the definitions `engine_unused.json` and `bcl_unused.json` of the
  stripping tool fork, see below) + `zeroDeadData` + `reoptimize`.
- `aggressive-lean-assetbundles`: `aggressive-lean` with the part `assetbundles` and its packages, without `unused.txt`
  and the steps after the build: the second scene of the coverage of `unused.txt`.
- `aggressive-novector`: `aggressive` with the vector types of the BCL not accelerated (ILLink substitutions,
  `stripping/vector-substitutions.xml`). IL2CPP answers `false` to `IsHardwareAccelerated` anyway (the intrinsics of
  libil2cpp), so the linker only removes branches the player never runs.
- `aggressive-addressables`: `aggressive` with Addressables (the fork of the framework, the part `addressables`: the
  catalog, a prefab with a sprite and an additive scene from one group of bundles); of 2D only the sprites, as everywhere.

A variant changes itself for some versions with `overrides` (each has a `why`, written into the result):
`"unity": "6000."` (a prefix) or `"from": "7000.0.0a16"` (that version and later), then `packages` (entries of the
manifest replaced; `builtin` is the version the editor ships; `builtin+framework:<fork folder>` is the package the editor ships with the
patches of that fork of the framework, the diff of the fork since its upstream commit without `FORK.md`, copied into
`projects/<version>-<variant>/Patched/<name>`), `unitypackages` (files under `only` taken from a `.unitypackage` of a built-in package of the
editor), `keep` (submodules taken out of the stripping lists). Now:

- `base` on Unity 6: the built-in uGUI of the editor (2.6 in 6000.6, 2.7 in 6000.7) instead of the fork of Unity 7000 (which does not build there) and the
  TextMeshPro shaders of its essential resources (`AssetDatabase.ImportPackage` is asynchronous in batchmode).
- `base` from 7000.0.0a20: the built-in uGUI 7.0.0 of the editor with the patches of the fork. The fork is the uGUI of
  7000.0.0a10, and a20 ships new code under the same version (TextMesh Pro, layout, event system); the rows of a11 to a16
  were measured with the fork, so with the uGUI of a10.
- `aggressive` from 7000.0.0a16: the submodule `ICU` kept. The .NET 10 BCL loads ICU at the start of the player
  (the AppContext setup of IL2CPP with `Il2CppData/Icu/icudt.dat`); with ICU stripped the player stops with
  `Unable to load required ICU Globalization data`.

A new variant is a json in `variants/` (and a list in `stripping/` or a part in `parts/` when it needs one); it is
measured on the next run of a version. A change of `base.txt` follows the list of the games: copy it from the game
and measure every version again, or the versions are no longer comparable.

## Finding what a variant breaks

`tools/measure.py <version> <variant> --trace-missing` builds with `MissingSubmoduleErrorHandling = LogError`: every
call of a stripped submodule is logged in the console and printed with the smoke run (not written to `results/`, the
logging adds code). A smoke run that times out without the ready line is such a call that did not return.

## Finding what else can be stripped

`tools/measure.py <version> <variant> --profile` builds the variant without the submodule stripping, instruments the
build with the submodule profiling of the stripping tool (`WebBuildProcessor.InstrumentBuild`), runs it until the ready
line and takes the report of the page (the submodules the scene called). `profiles/<version>/<variant>.json` lists:
`candidates` (never called, not stripped by the lists of the variant yet), `calledButStripped` (called, but the lists
strip them: the `Ignore` of the stripping keeps the page alive), `used`, `unusedStripped`. A candidate was only not
called by this scene: it goes into a list and the measured build checks it (smoke ready, `smoke.png`). The profiling
build replaces the build of the project: measure the variant again after it.

Below the submodules, by the functions:
- `tools/coverage.py <version> <variant>...` writes `projects/<version>-<variant>/called.txt`, the wasm functions the
  scene calls (`wasm-split --instrument` of the wasm before the stripping, run by `smoke.mjs --wasm-profile`).
- `tools/strip_simulation.py <version> <variant>` repeats the submodule stripping of the last build outside Unity with
  the names kept (the same code section byte for byte): `StripSimulation/survivors.tsv` lists the functions that
  survive it, with their size.
- `tools/unused_definition.py <version> <project>...` writes `engine_unused.json` and `bcl_unused.json` of the stripping
  tool fork: groups of code (a pattern of the names, or an assembly and a pattern of the C# signatures of the MethodMap
  of IL2CPP) whose functions of at least 200 bytes no `called.txt` of the given projects lists. A group goes there only
  when its reason holds for a game too (the description of each); `stripping/unused.txt` names them.

## Dead data of the wasm

The stripping removes functions, never their data: tables of ICU, of the texture decoders, the strings of the stripped
code stay in the data section. `tools/dead_data.py <version> <variant> [--smoke]` links the player of the last build
again with `--emit-relocs` (the `Link_WebGL_wasm` action of the Bee graph, `EMCC_DEBUG` keeps the output of wasm-ld in
`DeadData/`), checks that its data is the data of the build byte for byte, and follows the relocations: a data symbol
is live when a constant of the shipped code is the address of one of its relocations or falls inside it (binaryen
folds field offsets into addresses), unless the value is a literal of the code (`0x10FFFF` in the middle of a table
of ICU) far from every relocation; then whatever a live symbol points to. The rest is zeroed in place (no address
moves). `DeadData/dead.txt` lists the zeroed symbols and, first, every one decided by the literal rule: read it after
a new Unity version. The variant option `zeroDeadData` runs it after the build.

## What the scene runs

Every variant runs `Measurement.MeasurementRun`: the reactive primitives of `common`, UniTask, MemoryPack (the trimmed
fork) and the BCL the games use (collections, LINQ, formatting and parsing of numbers, `StringBuilder`, UTF-8,
exceptions, `TaskCompletionSource`), 64 animated sprites, 16 bodies of 2D physics, an imported clip played by an audio
source, and the parts of the variant. It draws with a minimal SRP (the games draw with their own). The assets are made
from code by each editor (`MeasurementAssets`), so no serialized asset of one version is upgraded by another.

The smoke run waits for the line `[measurement] ready ...` in the console; a part throws when its feature does not
work (no body fell, TextMeshPro laid out no characters). The stripping handles a call of a stripped submodule as
`Ignore` (as pixel-swarm), so a feature broken by the stripping shows as a failed check or in `smoke.png`, not as an error.

Not in the scene, so not in the numbers (Addressables only in `aggressive-addressables`): the client core of the framework (container, scopes), the
modules and the platforms. The numbers are the engine, the BCL and the common libraries; a game adds its own code and
assets on top.

## Numbers

- **Download**: `.wasm` + `.data` + `.framework.js` + `.loader.js` as served (brotli, `webGLCompressionFormat` 0 of
  the profile) and the files of `StreamingAssets` (the catalog and the bundles of Addressables, served as they are). The external symbols file is not loaded by the page and not counted.
- **wasm raw** and the sections (`code`, `data`): the module after decompression. The IL2CPP metadata (types, strings)
  is in the `.data` file, together with the scene and the assets.
- **wasm before submodule stripping**: the backup the stripping tool keeps in
  `Library/com.unity.web.stripping-tool/`; the difference is what the submodule list removes.
- **Contents of the data file**: the files of the `UnityWebData1.0` container, each compressed alone (an estimate):
  the scene and assets, the IL2CPP metadata (`global-metadata.dat`), the ICU data of the .NET 10 BCL
  (`Il2CppData/Icu/icudt.dat`), the resources of the assemblies.
- **Managed assemblies**: `Library/Bee/artifacts/WebGL/ManagedStripped/**/*.dll` after the IL2CPP linker
  (managed stripping High): the .NET libraries before they become C++ (`mscorlib` before .NET 10,
  `System.Private.CoreLib` and the facades with it).
