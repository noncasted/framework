using Internal;
using UnityEngine;

namespace Global.Audio
{
    // One music source with its fade: the volume of the source is line * clip * fade.
    internal sealed class MusicChannel
    {
        public MusicChannel(AudioSource source)
        {
            _source = source;
        }

        private readonly AudioSource _source;

        private Sound _sound;
        private float _fade;
        private float _target;
        private float _speed;

        // The request that owns the track: a lifetime of an older one does not fade it out.
        public long Order { get; private set; }

        public bool IsRising => _sound != null && Mathf.Approximately(_target, 1f) == true;

        public void TakeOver(long order)
        {
            Order = order;
        }

        public bool IsPlaying(Sound sound)
        {
            return IsRising == true && _source.isPlaying == true && _source.clip == sound.Clip;
        }

        public void Start(Sound sound, float fadeIn)
        {
            _sound = sound;
            _fade = 0f;

            _source.Stop();
            _source.loop = true;
            _source.clip = sound.Clip;
            _source.Play();

            FadeTo(1f, fadeIn);
        }

        public void FadeTo(float target, float time)
        {
            if (_sound == null)
                return;

            _target = target;

            if (time <= 0f)
            {
                _fade = target;
                _speed = 0f;
                StopIfSilent();
                return;
            }

            _speed = 1f / time;
        }

        // True when the fade has changed and the volume has to be applied.
        public bool Step(float delta)
        {
            if (_sound == null)
                return false;

            // Faded out from where it started: no step is left, the source still gives its voice back.
            if (Mathf.Approximately(_fade, _target) == true)
            {
                StopIfSilent();
                return false;
            }

            _fade = Mathf.MoveTowards(_fade, _target, _speed * delta);
            StopIfSilent();
            return true;
        }

        public void ApplyVolume(float line)
        {
            if (_sound == null)
            {
                _source.volume = 0f;
                return;
            }

            _source.volume = line * _sound.Volume * _fade;
        }

        private void StopIfSilent()
        {
            if (_fade > 0f || _target > 0f)
                return;

            _source.Stop();
            _source.clip = null;
            _sound = null;
        }
    }
}
