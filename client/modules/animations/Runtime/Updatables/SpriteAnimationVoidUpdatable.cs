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

        public void Start(float time)
        {
            _frameTime = time / _frameProvider.FrameCount;
            _index = 0;
            _timer = 0;
        }

        // The frame time remainder carries over, and a long delta skips several frames: the animation
        // lasts its own time at any frame rate. Durations such as reload rely on this.
        public bool Update(float deltaTime)
        {
            _timer += deltaTime;

            while (_timer >= _frameTime)
            {
                _timer -= _frameTime;
                _index++;

                if (_index >= _frameProvider.FrameCount)
                    return true;
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