namespace Internal.EditorTests.Content
{
    // The bundles the editor builds before the Play Mode tests of the bundles and the loader (ContentBundleTestBuild),
    // relative to the folder of the project.
    public static class ContentTestPaths
    {
        public const string Output = "Temp/ContentBundleTests";

        // The folder BuildAssetBundles writes.
        public const string Built = Output + "/Built";

        // The files of the bundles as a player reads them in place (ContentShipment), and the json of the catalog.
        public const string Folder = Output + "/Content";
        public const string Catalog = Output + "/catalog.json";

        // The main bundle: a root with a content reference to its target and a content scene, and the ContentRoot of the
        // group tests (one group loaded on Retain, one preloaded).
        public const string Name = "Tests";

        // A second bundle (AddContentBundle): a root with a content reference to its own target, whose direct reference is
        // the root of the shared bundle, and a content scene of its own.
        public const string ExtraName = "TestsExtra";
        public const string SharedName = "TestsShared";

        // The sources of the bundles, kept in the project until the run ends: Play Mode of the editor loads them
        // (EditorContentSource).
        public const string Assets = "Assets/ContentBundleTestsTemp";
        public const string TargetAsset = Assets + "/Target.asset";
        public const string SceneAsset = Assets + "/Level.unity";
        public const string ReferencesAsset = Assets + "/References.asset";
        public const string ExtraTargetAsset = Assets + "/ExtraTarget.asset";
        public const string ExtraSceneAsset = Assets + "/ExtraLevel.unity";
        public const string ExtraReferencesAsset = Assets + "/ExtraReferences.asset";
        public const string SharedAsset = Assets + "/Shared.asset";
        public const string ContentRootAsset = Assets + "/ContentRoot.asset";
        public const string GroupAsset = Assets + "/Group.asset";
        public const string PreloadedGroupAsset = Assets + "/Preloaded.asset";
    }
}
