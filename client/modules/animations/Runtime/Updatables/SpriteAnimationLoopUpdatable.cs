using System;

namespace Internal
{
    public class SpriteAnimationLoopUpdatable : IUpdatableSpriteAnimation
    {
        public SpriteAnimationLoopUpdatable(SpriteAnimationVoidUpdatable voidUpdatable)
        {
            _voidUpdatable = voidUpdatable;
        }

        private readonly SpriteAnimationVoidUpdatable _voidUpdatable;

        private float _time;

        public void Start(float time)
        {
            // A round of no time never ends: the wrap would spin forever.
            if (time <= 0f)
                throw new ArgumentOutOfRangeException(nameof(time), time, "[Animations] The time of a looped sprite animation");

            _time = time;
            _voidUpdatable.Start(_time);
        }

        // Wraps inside the void animation: the time left over at the end of a round goes into the next one.
        public bool Update(float deltaTime)
        {
            _voidUpdatable.UpdateLooped(deltaTime);
            return false;
        }

        public void Dispose()
        {

        }
    }
}