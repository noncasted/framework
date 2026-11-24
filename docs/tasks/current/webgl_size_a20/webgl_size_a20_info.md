# webgl_size_a20: the WebGL build of Unity 7000.0.0a20 down to about 2.8-2.9 MB brotli

Started 2026-11-19 in client/measurements. Prose with the user in Russian, code and docs in English. Nothing is committed
yet: every change below is in the working tree of main (the staged file `asdf` in the root is not ours, leave it).

## Goal
The user wants `aggressive-lean` of 7000.0.0a20 at about 2.8-2.9 MB **brotli** (they asked brotli this time; the memory
`build-size-gzip-only` says gzip for comparisons: give both). a10 aggressive-size was 2,495 KiB brotli.

## Where we are (2026-11-22, results/7000.0.0a20/aggressive-lean.json, smoke ready)
**2,874 KiB brotli** (wasm 1,974, data 811, framework.js 62, loader 27); gzip 3,795 (wasm 2,710, data 1,004).
The steps are the table "aggressive-lean of a20 below 2.9 MB brotli" of ANALYSIS.md:
3,195 -> MemoryPack 3,161 -> unused.txt 3,084 -> zeroDeadData 2,957 -> vector substitutions 2,918 -> reoptimize 2,899 ->
Content Load Manager + BCL Resources 2,874.

2026-11-24: stripping tool fork moved to 1.4.1 (in the working tree: the upstream files over 1.4.0, the patch of
`StrippingProjectSettings.cs` merged by hand; pristine 1.4.0 / 1.4.1 and the fork before in
`client/measurements/projects/stripping-tool-1.4.1/`). a20 lean with it 2,872 br / 3,793 gz (no change); a21 lean
2,899 br / 3,820 gz (wasm 1,990, data 820), smoke ready; table in ANALYSIS.md "7000.0.0a21 and stripping tool 1.4.1".
For the commits: the fork update is a pristine 1.4.1 commit, then the patches again (rule 3).

## Done (uncommitted), by concern for the commits
1. **MemoryPack fork** (`client/packages/com.cysharp.memorypack`): the second round of removed formatters (FORK.md row,
   `Source~/build.sh` standalone, DLL rebuilt), `releases/unreleased.md` step (search run in every game: nothing).
   EditMode of client/project with the new DLL: 1,299, 1,295 passed, 4 skipped (the benchmarks), 0 failed (2026-11-22).
2. **Stripping tool fork**: `SubmoduleDefinitions/bcl_globalization.json`, `engine_unused.json`, `bcl_unused.json` (+ .meta),
   FORK.md rows. Each fork change its own commit (rule 3).
3. **Measurements**: `tools/measure.py` (linkerArgs, dropData, zeroDeadData, reoptimize, builtin+framework:, scalar profile
   settings), `MeasurementBuild.cs` (-measurementLinkerArgs), `smoke.mjs --wasm-profile`, new tools `coverage.py`,
   `unused_definition.py`, `strip_simulation.py`, `dead_data.py`, `wasm_binary.py`, `bcl_icu_definition.py`; strip lists
   `invariant.txt`, `unused.txt`; `vector-substitutions.xml`; variants aggressive-invariant / -lean / -novector /
   -lean-assetbundles; part assetbundles with `DisableWriteTypeTree`; README, ANALYSIS (a20 lean section), RESULTS (report.py).
Cleaned: lean-substitutions.xml, the variants lean-novector and lean-map (the link map is not needed: the relocations
of `--emit-relocs` give more), their projects and results.

## Methods (all are tools now)
- `tools/strip_simulation.py <v> <variant>`: the submodule stripping outside Unity with names (exact: same code section).
  The id file of wasm-opt `--remove-functions=@file` is one id a line (a comma list removes only the first).
- `tools/coverage.py`, `tools/unused_definition.py`: called.txt and the definitions of groups no scene calls.
- `tools/dead_data.py <v> <variant> [--smoke]`: relink with `--emit-relocs` (EMCC_DEBUG keeps `emcc-01-base.wasm`), the
  live data symbols, zeroing; `DeadData/dead.txt` lists the literal-rule decisions first.
- Why the linker kept X: rerun UnityLinker by hand with the Action of the `UnityLinker` node of `Library/Bee/Player*.dag.json`
  plus `--enable-report --dump-dependencies`; follow `<edge b=... e=node>` back in `UnityLinker_Diagnostics/linkreport.xml`.

## Left (by expected gain), not started
1. A reduced copy of the CoreLib descriptor (`--ignore-descriptors` + a link.xml with what libil2cpp needs: the types of
   `InitIl2CppDefaults` in `il2cppOutput/wasm/cpp/Il2CppMetadataRegistration.cpp`, the methods `Runtime.cpp` looks up by
   name): up to -44 KiB; several IL2CPP rebuilds, failures only at run time.
2. `unity default resources` objects a game never references (13 primitive meshes 47 KiB br, legacy font 28, textures 20):
   needs the references of the game (bundles, CreatePrimitive, GetBuiltinResource).
3. Built-in render pipeline code (every game is on URP): needs the coverage of a game, URP shares shadow / culling code.
4. For the games (the user decides): zeroDeadData and reoptimize as post-build steps of deploy-client or the stripping
   fork; recompressing the .br files with window 24 (-1.4%, browsers decode it: smoke serves Content-Encoding br).
5. Not safe for games (silent default results with Ignore): Decimal, TimeZoneInfo, DateTime / Guid parsing.
