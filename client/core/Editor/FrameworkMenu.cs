namespace Internal
{
    /// <summary>
    /// Menu of the framework tools: one folder at the end of Tools, after the items of the game (default priority
    /// 1000), split by area. A submenu takes the priority of its first item and a gap over 10 draws a separator,
    /// so every item of the folder uses the priorities below. Menu attributes take constants only: the paths are
    /// composed here. Assemblies without a reference to the editor assembly (the runtime <c>ColorCatalog.Refresh</c>,
    /// the tests) repeat the path and the priority.
    /// </summary>
    public static class FrameworkMenu
    {
        public const string Root = "Tools/Framework/";

        public const string Build = Root + "Build/";
        public const string Codegen = Root + "Codegen/";
        public const string Container = Root + "Container/";
        public const string LifetimeGraph = Root + "Lifetime Graph/";

        // The items at the top of the folder.
        public const int Priority = 2000;

        // The subfolders and the windows, below a separator; also the first item of a subfolder.
        public const int GroupPriority = Priority + 20;

        // The items after one more separator: the generators below Regenerate All, the editor settings in the root.
        public const int TrailingPriority = GroupPriority + 20;
    }
}
