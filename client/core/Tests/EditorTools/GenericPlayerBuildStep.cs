using UnityEditor;

namespace Internal.EditorTests
{
    // An open generic step is a base for closed steps of a project, not a step itself: it cannot be created.
    public class GenericPlayerBuildStep<T> : IPlayerBuildStep
    {
        public void BeforeBuild(BuildTarget target)
        {
        }

        public void AfterBuild(BuildTarget target, bool succeeded)
        {
        }
    }
}
