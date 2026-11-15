using System.IO;
using Internal.EditorTests.Content;

namespace Internal.Tests
{
    // The bundles of ContentBundleTestBuild over a store: read in place, or fetched into a folder of the test.
    internal static class ContentTestSets
    {
        public const string FetchedFolder = ContentTestPaths.Output + "/Fetched";

        public static ContentCatalog Catalog => ContentCatalog.FromJson(File.ReadAllText(ContentTestPaths.Catalog));

        public static ContentBundleSet Local()
        {
            return new ContentBundleSet(Catalog, new LocalContentBundleStore(Path.GetFullPath(ContentTestPaths.Folder)));
        }

        public static ContentBundleSet Fetched(GatedContentBundleStore store)
        {
            return new ContentBundleSet(Catalog, store);
        }

        public static GatedContentBundleStore Store(bool isGated)
        {
            return new GatedContentBundleStore(Path.GetFullPath(ContentTestPaths.Folder), Path.GetFullPath(FetchedFolder), isGated);
        }

        public static void DeleteFetched()
        {
            if (Directory.Exists(FetchedFolder))
                Directory.Delete(FetchedFolder, true);
        }
    }
}
