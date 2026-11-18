using System.IO;
using Internal.EditorTests.Content;

namespace Internal.Tests
{
    // The bundles of ContentBundleTestBuild over a distribution: read in place, or fetched into a folder of the test.
    internal static class ContentTestStorages
    {
        public const string FetchedFolder = ContentTestPaths.Output + "/Fetched";

        public static ContentCatalog Catalog => ContentCatalog.FromJson(File.ReadAllText(ContentTestPaths.Catalog));

        public static ContentBundleStorage Local()
        {
            return new ContentBundleStorage(Catalog, new LocalContentDistribution(Path.GetFullPath(ContentTestPaths.Folder)));
        }

        public static ContentBundleStorage Fetched(GatedContentDistribution distribution)
        {
            return new ContentBundleStorage(Catalog, distribution);
        }

        public static GatedContentDistribution Distribution(bool isGated)
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
