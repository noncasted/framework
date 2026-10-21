# FORK: com.unity.web.stripping-tool 1.4.0

Base: registry package 1.4.0. Removed from the base: `Documentation~`, `Samples~`.

## Patches

| File | Why |
|---|---|
| `Editor/Core/WasmOpt.cs` | `--enable-exception-handling` for Emscripten 4 without Wasm 2023: otherwise binaryen -O3 drops every try/catch of a `-fwasm-exceptions` build and any exception crashes the player |
| `Editor/StrippingProjectSettings.cs` | Unity 7000 cannot load the settings asset on the first domain reload: retry with `delayCall` and recover the path when there is a single settings asset, otherwise post-build stripping is silently skipped |
