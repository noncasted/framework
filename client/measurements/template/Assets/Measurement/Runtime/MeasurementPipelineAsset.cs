using UnityEngine;
using UnityEngine.Rendering;

namespace Measurement
{
    /// <summary>
    ///     The render pipeline of the measured player. The games draw with their own small SRP (pixel-swarm:
    ///     GamePipeline), so the build keeps the scriptable pipeline code, not the built-in renderer.
    /// </summary>
    [CreateAssetMenu(menuName = "Measurement/Pipeline")]
    public sealed class MeasurementPipelineAsset : RenderPipelineAsset<MeasurementPipeline>
    {
        protected override RenderPipeline CreatePipeline()
        {
            return new MeasurementPipeline();
        }
    }
}
