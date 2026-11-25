using System;

namespace Internal
{
    public class UpdateUpdateProgression : UpdateProgressionBase, IUpdatable
    {
        public UpdateUpdateProgression(
            IReadOnlyLifetime lifetime,
            IUpdater updater,
            float time,
            Action<float> callback) : base(lifetime, updater, time, callback)
        {
        }

        protected override void Setup(IReadOnlyLifetime lifetime, IUpdater updater)
        {
            updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            PassDelta(delta);
        }
    }
}