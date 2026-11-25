namespace Global.Telemetry.Tests
{
    public class FakeMemoryReadings : ITelemetryMemoryReadings
    {
        public long Allocated { get; private set; }
        public long Reserved { get; private set; }
        public long MonoUsed { get; private set; }
        public long MonoHeap { get; private set; }
        public long Textures { get; private set; }
        public int Collections { get; private set; }
        public int TextureCount { get; private set; }

        public void SetTextureCount(int count)
        {
            TextureCount = count;
        }

        public void Set(long allocated, long reserved, long monoUsed, long monoHeap, long textures)
        {
            Allocated = allocated;
            Reserved = reserved;
            MonoUsed = monoUsed;
            MonoHeap = monoHeap;
            Textures = textures;
        }

        public void SetCollections(int collections)
        {
            Collections = collections;
        }
    }
}
