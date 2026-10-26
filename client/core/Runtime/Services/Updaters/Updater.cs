using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    [DisallowMultipleComponent]
    public class Updater : MonoBehaviour, IUpdater
    {
        private readonly ModifiableList<UpdateTargetHandle<IPreFixedUpdatable>> _preFixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IFixedUpdatable>> _fixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IPostFixedUpdatable>> _postFixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IPreUpdatable>> _preUpdate = new();
        private readonly ModifiableList<UpdateTargetHandle<IUpdatable>> _update = new();
        private readonly ModifiableList<UpdateTargetHandle<IGizmosUpdatable>> _gizmos = new();
        private readonly ModifiableList<UpdateTargetHandle<IPostUpdatable>> _postUpdate = new();
        private readonly ModifiableList<UpdateTargetHandle<ILateUpdatable>> _lateUpdate = new();

        private readonly ViewableProperty<float> _speed = new(1f);

        // The speed set by SetSpeed: Speed shows it while nobody holds a pause.
        private float _setSpeed = 1f;
        private int _pauses;

        public IViewableProperty<float> Speed => _speed;

        // Update and LateUpdate run on the unscaled clock multiplied by the speed of the updater: the speed is the
        // time scale of the game, Time.timeScale is not used for it. FixedUpdate runs when the physics step of Unity
        // comes, so its rate follows Time.timeScale, the delta of a step is the fixed one multiplied by the speed.
        private void Update()
        {
            var delta = Time.unscaledDeltaTime * Speed.Value;
            List<Exception> exceptions = null;

            UpdatePass.Run(_preUpdate, delta, static (u, d) => u.OnPreUpdate(d), ref exceptions);
            UpdatePass.Run(_update, delta, static (u, d) => u.OnUpdate(d), ref exceptions);
            UpdatePass.Run(_postUpdate, delta, static (u, d) => u.OnPostUpdate(d), ref exceptions);

            UpdatePass.Report(exceptions);
        }

        private void LateUpdate()
        {
            var delta = Time.unscaledDeltaTime * Speed.Value;
            List<Exception> exceptions = null;

            UpdatePass.Run(_lateUpdate, delta, static (u, d) => u.OnLateUpdate(d), ref exceptions);

            UpdatePass.Report(exceptions);
        }

        private void FixedUpdate()
        {
            var delta = Time.fixedDeltaTime * Speed.Value;
            List<Exception> exceptions = null;

            UpdatePass.Run(_preFixed, delta, static (u, d) => u.OnPreFixedUpdate(d), ref exceptions);
            UpdatePass.Run(_fixed, delta, static (u, d) => u.OnFixedUpdate(d), ref exceptions);
            UpdatePass.Run(_postFixed, delta, static (u, d) => u.OnPostFixedUpdate(d), ref exceptions);

            UpdatePass.Report(exceptions);
        }

        private void OnDrawGizmos()
        {
            List<Exception> exceptions = null;

            UpdatePass.Run(_gizmos, 0f, static (u, _) => u.OnGizmosUpdate(), ref exceptions);

            UpdatePass.Report(exceptions);
        }

        public void Add(IReadOnlyLifetime l, IPreUpdatable u) => UpdatePass.Add(l, u, _preUpdate);
        public void Add(IReadOnlyLifetime l, IUpdatable u) => UpdatePass.Add(l, u, _update);
        public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => UpdatePass.Add(l, u, _fixed);
        public void Add(IReadOnlyLifetime l, IPostFixedUpdatable u) => UpdatePass.Add(l, u, _postFixed);
        public void Add(IReadOnlyLifetime l, IGizmosUpdatable u) => UpdatePass.Add(l, u, _gizmos);
        public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u) => UpdatePass.Add(l, u, _preFixed);
        public void Add(IReadOnlyLifetime l, IPostUpdatable u) => UpdatePass.Add(l, u, _postUpdate);
        public void Add(IReadOnlyLifetime l, ILateUpdatable u) => UpdatePass.Add(l, u, _lateUpdate);

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
    }

    public readonly struct UpdateTargetHandle<T>
    {
        public UpdateTargetHandle(T target, IReadOnlyLifetime lifetime)
        {
            Target = target;
            Lifetime = lifetime;
        }

        public T Target { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}