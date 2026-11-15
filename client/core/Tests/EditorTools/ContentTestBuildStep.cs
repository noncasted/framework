namespace Internal.EditorTests
{
    // Counts the content builds (ContentBuildTests).
    public sealed class ContentTestBuildStep : IContentBuildStep
    {
        public static int Calls { get; private set; }

        public static void Reset()
        {
            Calls = 0;
        }

        public void BeforeContentBuild()
        {
            Calls++;
        }
    }
}
