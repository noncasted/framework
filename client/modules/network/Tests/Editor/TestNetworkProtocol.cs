using MemoryPack;
using NUnit.Framework;
using Shared;

namespace Internal.Tests
{
    // Registered before the tests of this namespace; the tests of another assembly that write through the real
    // ConnectionWriter (it serializes a message when it is written) call RegisterOnce from their own fixture.
    [SetUpFixture]
    public class TestNetworkProtocol
    {
        [OneTimeSetUp]
        public void Register()
        {
            RegisterOnce();
        }

        public static void RegisterOnce()
        {
            // In a game project its own union is registered on load and covers these tags plus the game ones:
            // replacing it would break the tests of the game that run after these.
            if (MemoryPackFormatterProvider.IsRegistered<INetworkContext>() == false)
                TestNetworkContextUnionInitializer.RegisterFormatter();

            if (MemoryPackFormatterProvider.IsRegistered<IEntityPayload>() == false)
                TestEntityPayloadUnionInitializer.RegisterFormatter();
        }
    }
}
