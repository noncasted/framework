using System.Collections.Generic;

namespace Internal.Tests
{
    // An updater driven by the test: Tick passes the delta as the Updater does (already multiplied by its speed).
    public sealed class ManualUpdater : IUpdater
    {
        private readonly List<(IReadOnlyLifetime Lifetime, IUpdatable Target)> _update = new();
        private readonly List<(IReadOnlyLifetime Lifetime, IFixedUpdatable Target)> _fixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IPostUpdatable>> _post = new();
        private readonly ViewableProperty<float> _speed = new(1f);

        public IViewableProperty<float> Speed => _speed;

        public int UpdateCount => _update.Count;

        public void SetSpeed(float speed) => _speed.Set(speed);
        public void Pause() => _speed.Set(0f);
        public void Continue() => _speed.Set(1f);

        public void Tick(float unscaledDelta)
        {
            var delta = unscaledDelta * _speed.Value;

            foreach (var (lifetime, target) in _update.ToArray())
            {
                if (lifetime.IsTerminated == false)
                    target.OnUpdate(delta);
            }
        }

        public void TickFixed(float delta)
        {
            foreach (var (lifetime, target) in _fixed.ToArray())
            {
                if (lifetime.IsTerminated == false)
                    target.OnFixedUpdate(delta * _speed.Value);
            }
        }

        // The PostUpdate pass of the Updater (UpdatePass): no copy of the list, so the allocation tests can run it.
        public void TickPost(float unscaledDelta)
        {
            List<System.Exception> exceptions = null;
            UpdatePass.Run(_post, unscaledDelta * _speed.Value, static (u, d) => u.OnPostUpdate(d), ref exceptions);
            UpdatePass.Report(exceptions);
        }

        public void Add(IReadOnlyLifetime l, IUpdatable u) => Add(l, u, _update);
        public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => Add(l, u, _fixed);

        public void Add(IReadOnlyLifetime l, IPreUpdatable updatable)
        {
        }

        public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u)
        {
        }

        public void Add(IReadOnlyLifetime l, IPostFixedUpdatable updatable)
        {
        }

        public void Add(IReadOnlyLifetime l, IGizmosUpdatable updatable)
        {
        }

        public void Add(IReadOnlyLifetime l, IPostUpdatable u) => UpdatePass.Add(l, u, _post);

        public void Add(IReadOnlyLifetime l, ILateUpdatable updatable)
        {
        }

        private static void Add<T>(IReadOnlyLifetime l, T target, List<(IReadOnlyLifetime, T)> list)
        {
            var entry = (l, target);
            list.Add(entry);
            l.Listen(() => list.Remove(entry));
        }
    }
}
