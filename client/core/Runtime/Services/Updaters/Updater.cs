using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    [DisallowMultipleComponent]
    public class Updater : MonoBehaviour, IUpdater
    {
        private readonly UpdateTargets _targets = new();

        private readonly UpdaterSpeed _speed = new();

        public IViewableProperty<float> Speed => _speed.Value;

        // Update and LateUpdate run on the unscaled clock multiplied by the speed of the updater: the speed is the
        // time scale of the game, Time.timeScale is not used for it. FixedUpdate runs when the physics step of Unity
        // comes, so its rate follows Time.timeScale, the delta of a step is the fixed one multiplied by the speed.
        private void Update()
        {
            var delta = Time.unscaledDeltaTime * Speed.Value;
            List<Exception> exceptions = null;

            UpdatePass.Run(_targets.PreUpdate, delta, static (u, d) => u.OnPreUpdate(d), ref exceptions);
            UpdatePass.Run(_targets.Update, delta, static (u, d) => u.OnUpdate(d), ref exceptions);
            UpdatePass.Run(_targets.PostUpdate, delta, static (u, d) => u.OnPostUpdate(d), ref exceptions);

            UpdatePass.Report(exceptions);
        }

        private void LateUpdate()
        {
            var delta = Time.unscaledDeltaTime * Speed.Value;
            List<Exception> exceptions = null;

            UpdatePass.Run(_targets.LateUpdate, delta, static (u, d) => u.OnLateUpdate(d), ref exceptions);

            UpdatePass.Report(exceptions);
        }

        private void FixedUpdate()
        {
            var delta = Time.fixedDeltaTime * Speed.Value;
            List<Exception> exceptions = null;

            UpdatePass.Run(_targets.PreFixed, delta, static (u, d) => u.OnPreFixedUpdate(d), ref exceptions);
            UpdatePass.Run(_targets.Fixed, delta, static (u, d) => u.OnFixedUpdate(d), ref exceptions);
            UpdatePass.Run(_targets.PostFixed, delta, static (u, d) => u.OnPostFixedUpdate(d), ref exceptions);

            UpdatePass.Report(exceptions);
        }

        private void OnDrawGizmos()
        {
            List<Exception> exceptions = null;

            UpdatePass.Run(_targets.Gizmos, 0f, static (u, _) => u.OnGizmosUpdate(), ref exceptions);

            UpdatePass.Report(exceptions);
        }

        public void Add(IReadOnlyLifetime l, IPreUpdatable u) => UpdatePass.Add(l, u, _targets.PreUpdate);
        public void Add(IReadOnlyLifetime l, IUpdatable u) => UpdatePass.Add(l, u, _targets.Update);
        public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => UpdatePass.Add(l, u, _targets.Fixed);
        public void Add(IReadOnlyLifetime l, IPostFixedUpdatable u) => UpdatePass.Add(l, u, _targets.PostFixed);
        public void Add(IReadOnlyLifetime l, IGizmosUpdatable u) => UpdatePass.Add(l, u, _targets.Gizmos);
        public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u) => UpdatePass.Add(l, u, _targets.PreFixed);
        public void Add(IReadOnlyLifetime l, IPostUpdatable u) => UpdatePass.Add(l, u, _targets.PostUpdate);
        public void Add(IReadOnlyLifetime l, ILateUpdatable u) => UpdatePass.Add(l, u, _targets.LateUpdate);

        public void SetSpeed(float speed) => _speed.Set(speed);
        public void Pause() => _speed.Pause();
        public void Continue() => _speed.Continue();
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