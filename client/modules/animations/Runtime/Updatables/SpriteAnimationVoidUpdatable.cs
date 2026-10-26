using System;

namespace Internal
{
    public class SpriteAnimationVoidUpdatable : IUpdatableSpriteAnimation
    {
        public SpriteAnimationVoidUpdatable(ISpriteAnimationRenderer renderer, IFrameProvider frameProvider)
        {
            _renderer = renderer;
            _frameProvider = frameProvider;
        }

        private readonly ISpriteAnimationRenderer _renderer;
        private readonly IFrameProvider _frameProvider;

        private float _frameTime;
        private float _timer;
        private int _index;

        // The first frame is drawn at once: drawn by the first Update, the previous sprite stayed for a frame.
        public void Start(float time)
        {
            var count = _frameProvider.FrameCount;

            if (count == 0)
                throw new InvalidOperationException("[Animations] The sprite animation has no frames");

            _frameTime = time / count;
            _index = 0;
            _timer = 0;

            _renderer.SetSprite(_frameProvider.GetFrame(0));
        }

        // The frame time remainder carries over, and a long delta skips several frames: the animation
        // lasts its own time at any frame rate. Durations such as reload rely on this.
        public bool Update(float deltaTime)
        {
            return Advance(deltaTime, false);
        }

        // The same, and the end of the round wraps to the first frame with the time left over.
        public void UpdateLooped(float deltaTime)
        {
            Advance(deltaTime, true);
        }

        private bool Advance(float deltaTime, bool isLooped)
        {
            _timer += deltaTime;

            while (_timer >= _frameTime)
            {
                _timer -= _frameTime;
                _index++;

                if (_index < _frameProvider.FrameCount)
                    continue;

                if (isLooped == false)
                    return true;

                _index = 0;
            }

            var sprite = _frameProvider.GetFrame(_index);
            _renderer.SetSprite(sprite);

            return false;
        }

        public void Dispose()
        {
        }
    }
}