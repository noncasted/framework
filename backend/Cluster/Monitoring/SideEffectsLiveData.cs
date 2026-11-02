namespace Cluster.Monitoring;

[GenerateSerializer]
public class SideEffectsSnapshotRequest { }

[GenerateSerializer]
public class SideEffectsSnapshotResponse
{
    [Id(0)] public string ServiceTag { get; init; } = "";
    [Id(1)] public Guid ServiceId { get; init; }
    [Id(2)] public SideEffectsLiveData Data { get; init; } = new();
}

[GenerateSerializer]
public class SideEffectsLiveData
{
    [Id(0)] public int QueueCount { get; init; }
    [Id(1)] public int ProcessingCount { get; init; }
    [Id(2)] public int RetryCount { get; init; }
    [Id(3)] public int DeadLetterCount { get; init; }
    [Id(4)] public List<SideEffectsThroughputEntry> ThroughputHistory { get; init; } = [];
    [Id(5)] public List<SideEffectsRetryEntry> RetryEntries { get; init; } = [];
}

[GenerateSerializer]
public class SideEffectsThroughputEntry
{
    [Id(0)] public DateTime Timestamp { get; init; }
    [Id(1)] public long Processed { get; init; }
    [Id(2)] public long Failed { get; init; }
}

[GenerateSerializer]
public class SideEffectsRetryEntry
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string TypeName { get; init; } = string.Empty;
    [Id(2)] public int RetryCount { get; init; }
    [Id(3)] public DateTime RetryAfter { get; init; }
    [Id(4)] public DateTime CreatedAt { get; init; }
}