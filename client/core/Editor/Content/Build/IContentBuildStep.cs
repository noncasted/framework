namespace Internal
{
    /// <summary>
    ///     A step that runs right before the content bundles are laid out and built (<see cref="ContentBuild" />): the
    ///     generators of the root assets write them here. Every non-abstract class implementing it is created with its
    ///     parameterless constructor and called in the order of the full type names. A step that throws fails the build.
    /// </summary>
    public interface IContentBuildStep
    {
        void BeforeContentBuild();
    }
}
