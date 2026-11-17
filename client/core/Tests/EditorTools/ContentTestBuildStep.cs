using System.Collections.Generic;

namespace Internal.EditorTests
{
    // Counts the content builds and leaves the bundles a test names out of them (ContentBuildTests).
    public sealed class ContentTestBuildStep : IContentBuildStep
    {
        private static readonly List<string> Excluded = new();

        public static int Calls { get; private set; }

        public static void Reset()
        {
            Calls = 0;
            Excluded.Clear();
        }

        public static void Exclude(string bundle)
        {
            Excluded.Add(bundle);
        }

        public void BeforeContentBuild(ContentBuildContext build)
        {
            Calls++;

            foreach (var bundle in Excluded)
                build.Exclude(bundle);
        }
    }
}
