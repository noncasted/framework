using System.IO;
using Internal.EditorTests.Content;

namespace Internal.Tests
{
    // The catalog of ContentBundleTestBuild and the distributions of its bundles: read in place, or fetched into a folder
    // of the test.
    internal static class ContentTestDistributions
    {
        public const string FetchedFolder = ContentTestPaths.Output + "/Fetched";

        public static ContentCatalog Catalog => ContentCatalog.FromJson(File.ReadAllText(ContentTestPaths.Catalog));

        public static LocalContentDistribution Local()
        {
            return new LocalContentDistribution(Path.GetFullPath(ContentTestPaths.Folder));
        }

        public static GatedContentDistribution Fetched(bool isGated)
        {
            return new GatedContentDistribution(Path.GetFullPath(ContentTestPaths.Folder), Path.GetFullPath(FetchedFolder), isGated);
        }

        public static void DeleteFetched()
        {
            if (Directory.Exists(FetchedFolder))
                Directory.Delete(FetchedFolder, true);
        }
    }
}
