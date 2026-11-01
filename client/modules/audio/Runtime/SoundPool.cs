using System;
using Internal;
using UnityEngine;

namespace Global.Audio
{
    // One-shot sounds on a fixed set of sources: the choice of a source (limits, a free one, the oldest one), the trim
    // and the volume of each source: line * clip * play.
    internal sealed class SoundPool
    {
        public SoundPool(AudioSource[] sources, AudioLineVolumes volumes)
        {
            _sources = sources;
            _volumes = volumes;

            _scales = new float[sources.Length];
            _limits = new SoundLimit[sources.Length];
            _lines = new AudioLine[sources.Length];
            _ends = new float[sources.Length];
            _orders = new long[sources.Length];

            for (var i = 0; i < sources.Length; i++)
            {
                _scales[i] = 1f;
                _lines[i] = AudioLine.SFX;
                _ends[i] = float.PositiveInfinity;
            }
        }

        private readonly AudioSource[] _sources;
        private readonly AudioLineVolumes _volumes;
        private readonly float[] _scales;
        private readonly SoundLimit[] _limits;
        private readonly AudioLine[] _lines;
        private readonly float[] _ends;
        private readonly long[] _orders;

        private long _order;

        public void Play(Sound sound, in SoundPlay play, IReadOnlyLifetime lifetime)
        {
            var index = PlayOnSource(sound, play);
            var order = _orders[index];

            // The source may play another sound by then: only this one is cut.
            lifetime.Listen(() => {
                if (_orders[index] != order)
                    return;

                _sources[index].Stop();
                _ends[index] = float.PositiveInfinity;
            });
        }

        public void Play(Sound sound, in SoundPlay play)
        {
            PlayOnSource(sound, play);
        }

        public void ApplyVolume()
        {
            for (var i = 0; i < _sources.Length; i++)
                _sources[i].volume = _volumes.Of(_lines[i]) * _scales[i];
        }

        // A trimmed sound stops at its end; the time of the source is the time of the clip, whatever the pitch.
        public void CutTrimmed()
        {
            for (var i = 0; i < _sources.Length; i++)
            {
                if (float.IsPositiveInfinity(_ends[i]) == true || _sources[i].time < _ends[i])
                    continue;

                _sources[i].Stop();
                _ends[i] = float.PositiveInfinity;
            }
        }

        private int PlayOnSource(Sound sound, in SoundPlay play)
        {
            var clip = sound.Clip;

            // Time of the clip to stop at; an untrimmed sound plays to its end.
            var end = float.PositiveInfinity;

            if (play.StartOffset > 0f || play.EndOffset > 0f)
            {
                var length = clip.length;

                if (play.StartOffset >= length - play.EndOffset)
                {
                    throw new ArgumentException(
                        $"[Audio] Trim {play.StartOffset} + {play.EndOffset} leaves nothing of {clip.name} ({length})",
                        nameof(play));
                }

                if (play.EndOffset > 0f)
                    end = length - play.EndOffset;
            }

            _volumes.Follow(play.Line);

            var index = SelectSource(play.Limit);
            var source = _sources[index];

            source.Stop();
            _scales[index] = sound.Volume * play.Volume;
            _limits[index] = play.Limit;
            _lines[index] = play.Line;
            _ends[index] = end;
            _orders[index] = ++_order;

            source.clip = clip;
            source.pitch = play.Pitch;
            source.panStereo = play.Pan;

            // A stopped source is at the start already: the time is set only for a trimmed start.
            if (play.StartOffset > 0f)
                source.time = play.StartOffset;

            source.volume = _volumes.Of(play.Line) * _scales[index];

            source.Play();
            return index;
        }

        // Over the limit - the oldest sound of the limit; otherwise a free source; with none free - the oldest sound.
        private int SelectSource(SoundLimit limit)
        {
            if (limit != null)
            {
                var count = 0;
                var oldest = -1;

                for (var i = 0; i < _sources.Length; i++)
                {
                    if (_limits[i] != limit || _sources[i].isPlaying == false)
                        continue;

                    count++;

                    if (oldest < 0 || _orders[i] < _orders[oldest])
                        oldest = i;
                }

                if (count >= limit.Max)
                    return oldest;
            }

            var first = 0;

            for (var i = 0; i < _sources.Length; i++)
            {
                if (_sources[i].isPlaying == false)
                    return i;

                if (_orders[i] < _orders[first])
                    first = i;
            }

            Debug.LogWarning($"[Audio] All {_sources.Length} sound sources play, the oldest sound is cut");
            return first;
        }
    }
}
