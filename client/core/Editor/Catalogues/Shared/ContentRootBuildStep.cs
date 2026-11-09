namespace Internal
{
    // The ContentRoot follows the catalogues and the scene folders of the build, whatever the editor scheduled before.
    public sealed class ContentRootBuildStep : IContentBuildStep
    {
        public void BeforeContentBuild(ContentDirectorySettings directory)
        {
            ContentRootGenerator.GenerateNow();
        }
    }
}
