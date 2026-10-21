using MemoryPack;

namespace Shared
{
    // Every message of the protocol. Union tags are declared next to the types, in each group file. Tag ranges of
    // INetworkContext, shared by the framework and every project:
    // - 0-249 framework (one byte on the wire): 0 EmptyResponse, 1 ErrorResponse, 10-19 session auth, 20-29 entity,
    //   30-39 object, 40-49 player, 50-59 service, 60-69 latency, 100-149 backend meta, 150-179 matchmaking,
    //   180-199 lobbies;
    // - 1000+ game.
    // A removed type leaves a hole: tags are never reused. A duplicated tag fails the MemoryPack generator.
    [MemoryPackable]
    public partial interface INetworkContext
    {
    }
}