using MemoryPack;
using Shared;

namespace Internal.Tests
{
    // The payloads of the entities: a union over IEntityPayload is declared by the game as well.
    [MemoryPackUnionFormatter(typeof(IEntityPayload))]
    [MemoryPackUnion(0, typeof(TestEntityPayload))]
    public partial class TestEntityPayloadUnion
    {
    }
}
