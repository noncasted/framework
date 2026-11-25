using Internal;

namespace Global.Telemetry.Tests
{
    // Remembers what was subscribed: the frames in these tests are given by hand.
    public class RecordingUpdater : IUpdater
    {
        public IReadOnlyLifetime UpdateLifetime { get; private set; }
        public IReadOnlyLifetime LateLifetime { get; private set; }

        public IViewableProperty<float> Speed { get; } = new ViewableProperty<float>(1f);

        public void SetSpeed(float speed)
        {
        }

        public void Pause()
        {
        }

        public void Continue()
        {
        }

        public void Add(IReadOnlyLifetime l, IUpdatable u)
        {
            UpdateLifetime = l;
        }

        public void Add(IReadOnlyLifetime l, ILateUpdatable updatable)
        {
            LateLifetime = l;
        }

        public void Add(IReadOnlyLifetime l, IPreUpdatable updatable)
        {
        }

        public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u)
        {
        }

        public void Add(IReadOnlyLifetime l, IFixedUpdatable u)
        {
        }

        public void Add(IReadOnlyLifetime l, IPostFixedUpdatable updatable)
        {
        }

        public void Add(IReadOnlyLifetime l, IGizmosUpdatable updatable)
        {
        }

        public void Add(IReadOnlyLifetime l, IPostUpdatable updatable)
        {
        }
    }
}
