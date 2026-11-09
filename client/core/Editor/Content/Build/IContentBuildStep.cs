namespace Internal
{
    /// <summary>
    ///     A step that runs right before the content directory is built (<see cref="ContentDirectoryBuilder" />): the
    ///     generators of the root assets write the roots of <see cref="ContentDirectorySettings.Roots" /> here. Every
    ///     non-abstract class implementing it is created with its parameterless constructor and called in the order of the
    ///     full type names. A step that throws fails the build.
    /// </summary>
    public interface IContentBuildStep
    {
        void BeforeContentBuild(ContentDirectorySettings directory);
    }
}
