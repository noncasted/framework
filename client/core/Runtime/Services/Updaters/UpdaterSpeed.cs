using System;

namespace Internal
{
    // The speed of an updater and the pauses over it.
    internal sealed class UpdaterSpeed
    {
        private readonly ViewableProperty<float> _value = new(1f);

        // The speed set by Set: Value shows it while nobody holds a pause.
        private float _set = 1f;
        private int _pauses;

        public IViewableProperty<float> Value => _value;

        // While paused the speed is kept for Continue.
        public void Set(float speed)
        {
            if (speed < 0f)
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "The speed of the updater is negative");

            _set = speed;

            if (_pauses == 0)
                _value.Set(speed);
        }

        // Pauses are counted: two owners pause independently, the time goes on after both continued.
        public void Pause()
        {
            _pauses++;
            _value.Set(0f);
        }

        public void Continue()
        {
            if (_pauses == 0)
                throw new InvalidOperationException("Continue of an updater that is not paused");

            _pauses--;

            if (_pauses == 0)
                _value.Set(_set);
        }
    }
}
