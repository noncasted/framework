namespace Internal
{
    // The frame cap of an unfocused Play window over the limit of the game: written only over a limit above the cap
    // (or none), taken back on focus to the value it replaced, unless the game changed the limit in between.
    internal sealed class PlayModeFrameCap
    {
        public PlayModeFrameCap(int cap)
        {
            _cap = cap;
        }

        private readonly int _cap;

        private int _replaced;
        private bool _isCapped;

        // The value to write into Application.targetFrameRate, null when nothing is written.
        public int? OnFocus(bool isFocused, int current)
        {
            if (isFocused == false)
            {
                if (_isCapped == true || IsAboveCap(current) == false)
                    return null;

                _replaced = current;
                _isCapped = true;
                return _cap;
            }

            if (_isCapped == false)
                return null;

            _isCapped = false;

            if (current != _cap)
                return null;

            return _replaced;
        }

        private bool IsAboveCap(int rate)
        {
            return rate <= 0 || rate > _cap;
        }
    }
}
