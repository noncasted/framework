using Internal;

namespace Global.UI.Tests
{
    // The tests call OnUpdate of the navigation themselves.
    public class NoUpdater : IUpdater
    {
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

        public void Add(IReadOnlyLifetime l, ILateUpdatable updatable)
        {
        }
    }
}
