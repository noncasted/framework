using System.Collections.Generic;

namespace Internal
{
    // Measurements of the offset between the engine frames and the profiler frames, in Play Mode.
    internal sealed class ProfilerFrameOffsets
    {
        /// <summary>How many offset measurements to keep, to take the median and not drift on an outlier.</summary>
        private const int OffsetSamples = 32;

        private readonly List<int> _offsets = new();

        public void Clear()
        {
            _offsets.Clear();
        }

        public void Add(int offset)
        {
            _offsets.Add(offset);

            if (_offsets.Count > OffsetSamples)
                _offsets.RemoveAt(0);
        }

        /// <summary>
        /// Offset between <see cref="Time.frameCount"/> and the profiler frame counter. Takes the median
        /// of accumulated measurements: a single measurement can be off by a frame if the profiler callback
        /// arrived after the engine had already started the next frame.
        /// </summary>
        public bool TryGetOffset(out int offset)
        {
            offset = 0;

            if (_offsets.Count == 0)
                return false;

            var sorted = new List<int>(_offsets);
            sorted.Sort();

            offset = sorted[sorted.Count / 2];

            return true;
        }
    }
}
