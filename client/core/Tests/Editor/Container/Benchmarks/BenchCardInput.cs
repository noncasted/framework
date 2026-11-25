namespace Internal.Tests
{
    internal sealed class BenchCardInput : IBenchSetup
    {
        public readonly BenchRootInput Input;
        public readonly BenchMatchLoop Loop;
        public readonly BenchCardDefinition Definition;

        public BenchCardInput(
            BenchRootInput input,
            BenchMatchLoop loop,
            BenchCardDefinition definition)
        {
            Input = input;
            Loop = loop;
            Definition = definition;
        }
    }
}
