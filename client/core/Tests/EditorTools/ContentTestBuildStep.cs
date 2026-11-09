namespace Internal.EditorTests
{
    // Counts the content builds of ContentBuildTests; the content builds of the project pass by.
    public sealed class ContentTestBuildStep : IContentBuildStep
    {
        public static int Calls { get; private set; }

        public static void Reset()
        {
            Calls = 0;
        }

        public void BeforeContentBuild(ContentDirectorySettings directory)
        {
            if (directory.Name == ContentBuildTests.Name)
                Calls++;
        }
    }
}
