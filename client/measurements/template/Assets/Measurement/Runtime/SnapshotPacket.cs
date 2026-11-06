using System;
using System.Collections.Generic;
using MemoryPack;

namespace Measurement
{
    // A message shaped like the protocol of the games (shared/Protocol): the same member kinds and the same
    // MemoryPack formatters, so the build keeps the serializer code a game keeps.
    [MemoryPackable]
    public partial class SnapshotPacket
    {
        public Guid Id { get; set; }
        public DateTime Created { get; set; }
        public TimeSpan Duration { get; set; }
        public string Text { get; set; }
        public int[] Order { get; set; }
        public List<float> Samples { get; set; }
        public Dictionary<string, int> Points { get; set; }
        public int? Winner { get; set; }
    }
}
