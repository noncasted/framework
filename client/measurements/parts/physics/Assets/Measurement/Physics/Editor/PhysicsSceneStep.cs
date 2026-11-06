using UnityEngine;

namespace Measurement
{
    /// <summary> Adds <see cref="PhysicsPart" />: absent from the variants that strip the 2D physics. </summary>
    public sealed class PhysicsSceneStep : IMeasurementSceneStep
    {
        public void Build(MeasurementSceneContext context)
        {
            var physics = new GameObject("Physics").AddComponent<PhysicsPart>();
            physics.Init(context.Assets.Sprite);
            context.Run.AddPart(physics);
        }
    }
}
