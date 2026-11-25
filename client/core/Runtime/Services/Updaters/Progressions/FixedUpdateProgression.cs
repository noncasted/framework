using System;

namespace Internal
{
    public class FixedUpdateProgression : UpdateProgressionBase, IFixedUpdatable
    {
        public FixedUpdateProgression(
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

        public void OnFixedUpdate(float delta)
        {
            PassDelta(delta);
        }
    }
}
