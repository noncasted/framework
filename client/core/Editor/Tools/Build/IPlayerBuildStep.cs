using UnityEditor;

namespace Internal
{
    /// <summary>
    ///     A project step around the player builds of the framework platforms (<c>Internal.WebGlBuild</c> of
    ///     <c>platforms/web</c>): prepares content before the build and cleans it up after. Every non-abstract class implementing it is picked up by
    ///     <see cref="PlayerBuildSteps" />; it needs a parameterless constructor (the contract of Unity's
    ///     <c>IPreprocessBuildWithReport</c>). The step decides by the target what it does.
    ///     <para>
    ///         Unity's build callbacks do not cover this: <c>IPostprocessBuildWithReport</c> is skipped when the build
    ///         fails, so content added for the build would stay in the project. <see cref="AfterBuild" /> is called for
    ///         every step whose <see cref="BeforeBuild" /> was called - also when it threw, a later step threw or the
    ///         build failed - and before the batchmode exit.
    ///     </para>
    /// </summary>
    public interface IPlayerBuildStep
    {
        void BeforeBuild(BuildTarget target);

        /// <param name="succeeded">False when the build failed or did not start (a step or the build setup threw).</param>
        void AfterBuild(BuildTarget target, bool succeeded);
    }
}
