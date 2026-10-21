namespace Shared
{
    // Every message of the protocol (a request, a response, a push). The union over INetworkContext is declared by
    // the project, not here: its types live in two assemblies (the framework ones here, the game ones in the project
    // shared) and a [MemoryPackable] partial interface cannot span assemblies. The project declares
    //
    //     [MemoryPackUnionFormatter(typeof(INetworkContext))]
    //     [MemoryPackUnion(NetworkContextTags.EmptyResponse, typeof(EmptyResponse))]
    //     [MemoryPackUnion(NetworkContextTags.ErrorResponse, typeof(ErrorResponse))]
    //     public partial class NetworkContextUnion { }
    //
    // and adds the tags of its types next to them, in partial declarations of the same class. The generated
    // NetworkContextUnionInitializer.RegisterFormatter() must run before the first message: on .NET it is a module
    // initializer, in Unity the project calls it on load.
    //
    // A member of type INetworkContext needs [MemoryPackAllowSerialize]: the formatter is registered at runtime.
    public interface INetworkContext
    {
    }

    // Tag ranges of the INetworkContext union, shared by the framework and every project:
    // - 0-249 framework (one byte on the wire): 0 EmptyResponse, 1 ErrorResponse, 10-19 session auth, 20-29 entity,
    //   30-39 object, 40-49 player, 50-59 service, 60-69 latency, 100-149 backend meta, 150-179 matchmaking,
    //   180-199 lobbies;
    // - 1000+ game.
    // A removed type leaves a hole: tags are never reused. A duplicated tag fails the MemoryPack generator.
    public static class NetworkContextTags
    {
        public const ushort EmptyResponse = 0;
        public const ushort ErrorResponse = 1;

        public const ushort GameFirst = 1000;
    }
}
