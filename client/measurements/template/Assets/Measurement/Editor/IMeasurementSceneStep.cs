namespace Measurement
{
    /// <summary>
    ///     Adds an optional part of a variant (a folder of <c>parts/</c>) to the measured scene. Found by
    ///     <see cref="UnityEditor.TypeCache" />: a variant without the part has no such type in its project.
    /// </summary>
    public interface IMeasurementSceneStep
    {
        void Build(MeasurementSceneContext context);
    }
}
