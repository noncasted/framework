namespace Internal
{
    // The ContentRoot follows the catalogues and the scene folders of the build, whatever the editor scheduled before:
    // generated before the bundles are laid out (a bundle holds it when it is among its roots).
    public sealed class ContentRootBuildStep : IContentBuildStep
    {
        public void BeforeContentBuild()
        {
            ContentRootGenerator.GenerateNow();
        }
    }
}
