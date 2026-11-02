using System;
using System.Collections.Generic;

namespace Internal
{
    // An updater inside another one: its targets get the delta of the parent (already multiplied by the speed of the
    // parent) multiplied by the own speed of the proxy, so the proxy pauses and slows down a part of the game.
    public class UpdaterProxy :
        IUpdater,
        IUpdatable,
        IPreUpdatable,
        IPreFixedUpdatable,
        IFixedUpdatable,
        IPostFixedUpdatable,
        IGizmosUpdatable,
        IPostUpdatable,
        ILateUpdatable,
        IScopeSetup
    {
        private readonly UpdateTargets _targets = new();

        private readonly IUpdater _updater;
        private readonly UpdaterSpeed _speed = new();

        public IViewableProperty<float> Speed => _speed.Value;

        public UpdaterProxy(IUpdater updater)
        {
            _updater = updater;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, (IPreUpdatable)this);
            _updater.Add(lifetime, (IUpdatable)this);
            _updater.Add(lifetime, (IFixedUpdatable)this);
            _updater.Add(lifetime, (IPostFixedUpdatable)this);
            _updater.Add(lifetime, (IGizmosUpdatable)this);
            _updater.Add(lifetime, (IPreFixedUpdatable)this);
            _updater.Add(lifetime, (IPostUpdatable)this);
            _updater.Add(lifetime, (ILateUpdatable)this);
        }

        public void Add(IReadOnlyLifetime l, IPreUpdatable u) => UpdatePass.Add(l, u, _targets.PreUpdate);
        public void Add(IReadOnlyLifetime l, IUpdatable u) => UpdatePass.Add(l, u, _targets.Update);
        public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => UpdatePass.Add(l, u, _targets.Fixed);
        public void Add(IReadOnlyLifetime l, IPostFixedUpdatable u) => UpdatePass.Add(l, u, _targets.PostFixed);
        public void Add(IReadOnlyLifetime l, IGizmosUpdatable u) => UpdatePass.Add(l, u, _targets.Gizmos);
        public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u) => UpdatePass.Add(l, u, _targets.PreFixed);
        public void Add(IReadOnlyLifetime l, IPostUpdatable u) => UpdatePass.Add(l, u, _targets.PostUpdate);
        public void Add(IReadOnlyLifetime l, ILateUpdatable u) => UpdatePass.Add(l, u, _targets.LateUpdate);

        public void OnPreUpdate(float delta) => Run(_targets.PreUpdate, delta, static (u, d) => u.OnPreUpdate(d));
        public void OnUpdate(float delta) => Run(_targets.Update, delta, static (u, d) => u.OnUpdate(d));
        public void OnPostUpdate(float delta) => Run(_targets.PostUpdate, delta, static (u, d) => u.OnPostUpdate(d));
        public void OnLateUpdate(float delta) => Run(_targets.LateUpdate, delta, static (u, d) => u.OnLateUpdate(d));
        public void OnPreFixedUpdate(float delta) => Run(_targets.PreFixed, delta, static (u, d) => u.OnPreFixedUpdate(d));
        public void OnFixedUpdate(float delta) => Run(_targets.Fixed, delta, static (u, d) => u.OnFixedUpdate(d));
        public void OnPostFixedUpdate(float delta) => Run(_targets.PostFixed, delta, static (u, d) => u.OnPostFixedUpdate(d));
        public void OnGizmosUpdate() => Run(_targets.Gizmos, 0f, static (u, _) => u.OnGizmosUpdate());

        public void SetSpeed(float speed) => _speed.Set(speed);
        public void Pause() => _speed.Pause();
        public void Continue() => _speed.Continue();

        private void Run<T>(ModifiableList<UpdateTargetHandle<T>> list, float delta, Action<T, float> update)
        {
            List<Exception> exceptions = null;

            UpdatePass.Run(list, delta * _speed.Value.Value, update, ref exceptions);
            UpdatePass.Report(exceptions);
        }
    }
}
