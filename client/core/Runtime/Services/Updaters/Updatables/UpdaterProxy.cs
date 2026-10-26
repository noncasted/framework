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
        private readonly ModifiableList<UpdateTargetHandle<IPreFixedUpdatable>> _preFixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IFixedUpdatable>> _fixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IPostFixedUpdatable>> _postFixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IPreUpdatable>> _preUpdate = new();
        private readonly ModifiableList<UpdateTargetHandle<IUpdatable>> _update = new();
        private readonly ModifiableList<UpdateTargetHandle<IGizmosUpdatable>> _gizmos = new();
        private readonly ModifiableList<UpdateTargetHandle<IPostUpdatable>> _postUpdate = new();
        private readonly ModifiableList<UpdateTargetHandle<ILateUpdatable>> _lateUpdate = new();

        private readonly IUpdater _updater;
        private readonly ViewableProperty<float> _speed = new(1f);

        // The speed set by SetSpeed: Speed shows it while nobody holds a pause.
        private float _setSpeed = 1f;
        private int _pauses;

        public IViewableProperty<float> Speed => _speed;

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

        public void Add(IReadOnlyLifetime l, IPreUpdatable u) => UpdatePass.Add(l, u, _preUpdate);
        public void Add(IReadOnlyLifetime l, IUpdatable u) => UpdatePass.Add(l, u, _update);
        public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => UpdatePass.Add(l, u, _fixed);
        public void Add(IReadOnlyLifetime l, IPostFixedUpdatable u) => UpdatePass.Add(l, u, _postFixed);
        public void Add(IReadOnlyLifetime l, IGizmosUpdatable u) => UpdatePass.Add(l, u, _gizmos);
        public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u) => UpdatePass.Add(l, u, _preFixed);
        public void Add(IReadOnlyLifetime l, IPostUpdatable u) => UpdatePass.Add(l, u, _postUpdate);
        public void Add(IReadOnlyLifetime l, ILateUpdatable u) => UpdatePass.Add(l, u, _lateUpdate);

        public void OnPreUpdate(float delta) => Run(_preUpdate, delta, static (u, d) => u.OnPreUpdate(d));
        public void OnUpdate(float delta) => Run(_update, delta, static (u, d) => u.OnUpdate(d));
        public void OnPostUpdate(float delta) => Run(_postUpdate, delta, static (u, d) => u.OnPostUpdate(d));
        public void OnLateUpdate(float delta) => Run(_lateUpdate, delta, static (u, d) => u.OnLateUpdate(d));
        public void OnPreFixedUpdate(float delta) => Run(_preFixed, delta, static (u, d) => u.OnPreFixedUpdate(d));
        public void OnFixedUpdate(float delta) => Run(_fixed, delta, static (u, d) => u.OnFixedUpdate(d));
        public void OnPostFixedUpdate(float delta) => Run(_postFixed, delta, static (u, d) => u.OnPostFixedUpdate(d));
        public void OnGizmosUpdate() => Run(_gizmos, 0f, static (u, _) => u.OnGizmosUpdate());

        // While paused the speed is kept for Continue.
        public void SetSpeed(float speed)
        {
            if (speed < 0f)
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "The speed of the updater is negative");

            _setSpeed = speed;

            if (_pauses == 0)
                _speed.Set(speed);
        }

        // Pauses are counted: two owners pause independently, the time goes on after both continued.
        public void Pause()
        {
            _pauses++;
            _speed.Set(0f);
        }

        public void Continue()
        {
            if (_pauses == 0)
                throw new InvalidOperationException("Continue of an updater that is not paused");

            _pauses--;

            if (_pauses == 0)
                _speed.Set(_setSpeed);
        }

        private void Run<T>(ModifiableList<UpdateTargetHandle<T>> list, float delta, Action<T, float> update)
        {
            List<Exception> exceptions = null;

            UpdatePass.Run(list, delta * _speed.Value, update, ref exceptions);
            UpdatePass.Report(exceptions);
        }
    }
}
