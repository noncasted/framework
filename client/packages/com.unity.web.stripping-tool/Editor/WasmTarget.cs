using System;

namespace Unity.Web.Stripping.Editor
{
    [Flags]
    enum WasmTarget
    {
        Wasm2023Flag = 1 << 0,
        MultithreadedFlag = 1 << 1,
        Wasm64Flag = 1 << 2,

        WasmBase = 0,
        Wasm2023 = Wasm2023Flag,
        Wasm2023Multithreaded = Wasm2023Flag | MultithreadedFlag,
        Wasm64 = Wasm64Flag | Wasm2023Flag,
    }
}
