namespace Internal
{
    // A step that runs right before the content bundles are laid out and built (ContentBuild): the generators of the root
    // assets write them here, and a step leaves bundles out of this build (ContentBuildContext.Exclude). Every non-abstract
    // class implementing it is created with its parameterless constructor and called in the order of the full type names.
    // A step that throws fails the build. What needs the final set of the bundles left out is an IContentLayoutStep.
    public interface IContentBuildStep
    {
        void BeforeContentBuild(ContentBuildContext build);
    }
}
