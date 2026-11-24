# FORK: com.unity.web.stripping-tool 1.4.1

Base: registry package 1.4.1. Removed from the base: `Documentation~`, `Samples~`.

## Patches

| File | Why |
|---|---|
| `Editor/Core/WasmOpt.cs` | `--enable-exception-handling` for Emscripten 4 without Wasm 2023: otherwise binaryen -O3 drops every try/catch of a `-fwasm-exceptions` build and any exception crashes the player |
| `Editor/StrippingProjectSettings.cs` | `DefaultSettingsCreator` does not search the project when the active settings are alive: the search took half of every refresh of the database (27 of 55 ms). |
| `Editor/StrippingProjectSettings.cs` | Unity 7000 cannot load the settings asset on the first domain reload: retry with `delayCall` and recover the path when there is a single settings asset, otherwise post-build stripping is silently skipped |
| `Editor/StrippingProjectSettings.cs` | `LoadSettings` is public: the batchmode build of the framework (`Internal.WebGlBuild`) loads the settings itself before building, the `delayCall` retry may not run before `-executeMethod` |
| `SubmoduleDefinitions/bcl_globalization.json` | the submodule `BCL Globalization ICU`: the ICU of the .NET BCL of IL2CPP and System.Globalization.Native (the `ICU` of Unity is the ICU of the engine), for a build with the BCL in the invariant globalization mode; written by `client/measurements/tools/bcl_icu_definition.py` from a build of 7000.0.0a20 |
| `SubmoduleDefinitions/engine_unused.json` | engine code outside the submodules of Unity that the measured scenes never call and no game needs: Safe Binary Read (type trees other than the player's), Graphics State Collection, Platform Audio (the native audio devices), Content Load Backend and Content Load Manager (the content files of `Unity.Loading`), Text From Binary Renderer; the functions of at least 200 bytes of each group, written by `client/measurements/tools/unused_definition.py` from builds of 7000.0.0a20, the reason in the description of each submodule |
| `SubmoduleDefinitions/bcl_unused.json` | the same for the .NET BCL: BCL Event Source (EventSource is not supported on IL2CPP), BCL JavaScript Interop (JSImport / JSExport), BCL Resources (the lookup of the resource strings, only with the linker feature `UseSystemResourceKeys`) |
