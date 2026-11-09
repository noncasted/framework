namespace Internal.EditorTests.Content
{
    // The content directory the editor builds before the Play Mode tests of the stores (ContentDirectoryTestBuild),
    // relative to the folder of the project.
    public static class ContentTestPaths
    {
        public const string Output = "Temp/ContentDirectoryTests";
        public const string Name = "Tests";
        public const string Folder = Output + "/" + Name;

        // The json of its ContentIndex, next to the folder: an extra file inside would be listed by the registration.
        public const string Index = Output + "/" + Name + ".json";

        // The GUID of the asset behind the lazy reference of the root.
        public const string TargetGuid = Output + "/" + Name + ".target";
    }
}
