using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public class SpriteAnimation : IAnimation, IUpdatable, IScopeSetup
    {
        public SpriteAnimation(
            IUpdater updater,
            ISpriteAnimationRenderer renderer,
            float defaultTime,
            Color color,
            IFrameProvider frameProvider)
        {
            _updater = updater;
            _renderer = renderer;
            _defaultTime = defaultTime;
            _color = color;
            _void = new SpriteAnimationVoidUpdatable(renderer, frameProvider);
            _stop = ManualStop;
        }

        private readonly IUpdater _updater;
        private readonly ISpriteAnimationRenderer _renderer;
        private readonly float _defaultTime;
        private readonly Color _color;
        private readonly SpriteAnimationVoidUpdatable _void;
        private readonly Action _stop;

        private SpriteAnimationLoopUpdatable _loop;
        private SpriteAnimationAsyncUpdatable _async;

        private IUpdatableSpriteAnimation _current;
        private IReadOnlyLifetime _lifetime;

        public bool IsPlaying => _current != null;
        public IUpdatableSpriteAnimation Current => _current;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void Play(IReadOnlyLifetime lifetime, float time = 0)
        {
            time = Prepare(time);
            _void.Start(time);
            _current = _void;
            Follow(lifetime);
        }

        public async UniTask PlayAwaited(IReadOnlyLifetime lifetime, float time = 0)
        {
            time = Prepare(time);
            _async ??= new SpriteAnimationAsyncUpdatable(_void);
            _current = _async;

            var process = _async.Process(time);
            Follow(lifetime);

            await process;
            _current = null;
        }

        public void PlayLooped(IReadOnlyLifetime lifetime, float time = 0)
        {
            time = Prepare(time);
            _loop ??= new SpriteAnimationLoopUpdatable(_void);
            _loop.Start(time);
            _current = _loop;
            Follow(lifetime);
        }

        // The previous play ends; returns the time of the new one.
        private float Prepare(float time)
        {
            _current?.Dispose();
            _renderer.SetColor(_color);

            return Mathf.Approximately(time, 0f) == true ? _defaultTime : time;
        }

        public void ManualStop()
        {
            _current?.Dispose();
            _current = null;
        }

        // The current play ends with its lifetime; the lifetime of an older play has no say any more.
        private void Follow(IReadOnlyLifetime lifetime)
        {
            _lifetime?.RemoveListener(_stop);
            _lifetime = lifetime;
            lifetime.Listen(_stop);
        }

        public void OnUpdate(float delta)
        {
            if (_current == null)
                return;

            var delete = _current.Update(delta);

            if (delete == true)
                _current = null;
        }
    }
}