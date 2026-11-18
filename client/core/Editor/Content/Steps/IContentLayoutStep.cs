namespace Internal
{
    // A step around the layout and the build of the content bundles (ContentBuild), after every IContentBuildStep and the
    // shared bundles (ContentSharedSync): the bundles left out of this build are final here (ContentBuildContext.ExcludedBundles),
    // whatever the order of the steps that left them out. Found and created as the IContentBuildSteps are. BeforeLayout runs
    // before the bundles are laid out, in the order of the full type names; AfterBundles after they are built, also when the
    // layout or the build failed, in the reverse order, for every step whose BeforeLayout returned. A step that throws fails
    // the build.
    public interface IContentLayoutStep
    {
        void BeforeLayout(ContentBuildContext build);

        void AfterBundles(ContentBuildContext build);
    }
}
