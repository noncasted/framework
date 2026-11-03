using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Global.Telemetry
{
    public class UnityTelemetryMemoryReadings : ITelemetryMemoryReadings
    {
        public long Allocated => Profiler.GetTotalAllocatedMemoryLong();
        public long Reserved => Profiler.GetTotalReservedMemoryLong();
        public long MonoUsed => Profiler.GetMonoUsedSizeLong();
        public long MonoHeap => Profiler.GetMonoHeapSizeLong();
        public long Textures => (long)Texture.currentTextureMemory;
        public int Collections => GC.CollectionCount(0);
        public int TextureCount => (int)(Texture.nonStreamingTextureCount + Texture.streamingTextureCount);
    }
}
