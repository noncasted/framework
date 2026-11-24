# FORK: com.unity.web.stripping-tool 1.4.1

Base: registry package 1.4.1. Removed from the base: `Documentation~`, `Samples~`.

## Patches

| File | Why |
|---|---|
| `Editor/Core/WasmOpt.cs` | `--enable-exception-handling` for Emscripten 4 without Wasm 2023: otherwise binaryen -O3 drops every try/catch of a `-fwasm-exceptions` build and any exception crashes the player |
| `Editor/StrippingProjectSettings.cs` | `DefaultSettingsCreator` does not search the project when the active settings are alive: the search took half of every refresh of the database (27 of 55 ms). |
| `Editor/StrippingProjectSettings.cs` | Unity 7000 cannot load the settings asset on the first domain reload: retry with `delayCall` and recover the path when there is a single settings asset, otherwise post-build stripping is silently skipped |
| `Editor/StrippingProjectSettings.cs` | `LoadSettings` is public: the batchmode build of the framework (`Internal.WebGlBuild`) loads the settings itself before building, the `delayCall` retry may not run before `-executeMethod` |
