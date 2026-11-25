namespace Internal.Tests
{
    internal sealed class BenchMatchEvents : IBenchSetup
    {
        public readonly BenchRootPublisher Publisher;

        public BenchMatchEvents(BenchRootPublisher publisher)
        {
            Publisher = publisher;
        }
    }
}
