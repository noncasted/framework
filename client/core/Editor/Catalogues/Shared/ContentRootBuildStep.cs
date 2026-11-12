using System.Linq;

namespace Internal
{
    // The ContentRoot follows the catalogues and the scene folders of the build, whatever the editor scheduled before:
    // generated before the build of the directory that has it among its roots, the other directories pass by.
    public sealed class ContentRootBuildStep : IContentBuildStep
    {
        public void BeforeContentBuild(ContentDirectorySettings directory)
        {
            if (directory.Roots.Contains(ContentRootGenerator.Path))
                ContentRootGenerator.GenerateNow();
        }
    }
}
