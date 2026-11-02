using Common;
using Shared;

namespace Infrastructure;

[GrainState(Table = "configs", State = "runtime_channel_config", Lookup = "RuntimeChannelConfig",
    Key = GrainKeyType.String)]
public class RuntimeChannelOptions
{
    public int ObserverKeepAliveMinutes { get; init; } = 3;
    public int CatchUpBufferSize { get; init; } = 1024;
    public int DeliveryTimeoutSeconds { get; init; } = 5;
}

public interface IRuntimeChannelConfig : IAddressableState<RuntimeChannelOptions>
{
}