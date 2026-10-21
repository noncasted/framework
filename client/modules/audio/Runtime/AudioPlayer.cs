using System;
using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Global.Audio
{
    public interface IAudioPlayer
    {
        void PlaySound(Sound sound);

        // One shot with its volume, pitch, pan and limit. Takes a free source of the pool; with none free it cuts the
        // oldest sound and logs a warning. Over play.Limit it cuts the oldest sound of the same limit instead.
        void PlaySound(Sound sound, in SoundPlay play);

        // The same, and the sound is cut at once when the lifetime terminates before the clip ends (a reload cut off
        // by a weapon switch). The lifetime should be short: it keeps a listener until it terminates.
        void PlaySound(Sound sound, in SoundPlay play, IReadOnlyLifetime lifetime);

        // Loops the track. The same track that already plays goes on (the menu calls it on every entry);
        // another one starts from its beginning and rises for fadeIn seconds while the old one fades out
        // over the same time (crossfade on two sources).
        void PlayLoopMusic(Sound sound, float fadeIn);

        // Fades out all music for fadeOut seconds and stops it; 0 - at once.
        void StopMusic(float fadeOut);
    }

    [DisallowMultipleComponent]
    public class AudioPlayer : MonoBehaviour, IAudioPlayer, IScopeSetupCompletion, IUpdatable
    {
        [SerializeField] private AudioSource[] _musicSources;
        [SerializeField] private AudioSource[] _soundSources;

        private IAudioVolume _volume;
        private IUpdater _updater;
        private MusicChannel[] _music;
        private int _activeMusic;
        private float[] _soundScales;
        private SoundLimit[] _soundLimits;
        private long[] _soundOrders;
        private long _soundOrder;

        private readonly Dictionary<AudioLine, float> _values = new();

        [Inject]
        public void Construct(IAudioVolume volume, IUpdater updater)
        {
            _volume = volume;
            _updater = updater;
        }

        // The volume is taken after the setup of the scope: the settings of the game fill IAudioVolume from their save
        // (or the defaults) in their setup. The settings sliders change it on the fly.
        public void OnSetupCompletion(IReadOnlyLifetime lifetime)
        {
            // Crossfade needs exactly two sources: the rising track and the fading one.
            if (_musicSources.Length != 2)
                throw new InvalidOperationException($"AudioPlayer needs 2 music sources, has {_musicSources.Length}");

            _music = new MusicChannel[_musicSources.Length];

            for (var i = 0; i < _music.Length; i++)
                _music[i] = new MusicChannel(_musicSources[i]);

            _soundScales = new float[_soundSources.Length];
            _soundLimits = new SoundLimit[_soundSources.Length];
            _soundOrders = new long[_soundSources.Length];

            for (var i = 0; i < _soundScales.Length; i++)
                _soundScales[i] = 1f;

            _volume.MasterVolume.View(lifetime, UpdateVolume);
            _volume.MusicVolume.View(lifetime, UpdateVolume);
            _volume.SoundsVolume.View(lifetime, UpdateVolume);

            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            var changed = false;

            foreach (var channel in _music)
                changed |= channel.Step(delta);

            if (changed == true)
                ApplyMusicVolume();
        }

        private void UpdateVolume()
        {
            var master = _volume.MasterVolume.Value;

            // Both lines are written before ApplyVolume: it reads them together.
            _values[AudioLine.Music] = _volume.MusicVolume.Value * master;
            _values[AudioLine.SFX] = _volume.SoundsVolume.Value * master;

            ApplyVolume();
        }

        private void ApplyVolume()
        {
            ApplyMusicVolume();

            for (var i = 0; i < _soundSources.Length; i++)
                _soundSources[i].volume = _values[AudioLine.SFX] * _soundScales[i];
        }

        private void ApplyMusicVolume()
        {
            foreach (var channel in _music)
                channel.ApplyVolume(_values[AudioLine.Music]);
        }

        public void PlaySound(Sound sound)
        {
            Play(sound, SoundPlay.Default);
        }

        public void PlaySound(Sound sound, in SoundPlay play)
        {
            Play(sound, play);
        }

        public void PlaySound(Sound sound, in SoundPlay play, IReadOnlyLifetime lifetime)
        {
            var index = Play(sound, play);
            var order = _soundOrders[index];

            // The source may play another sound by then: only this one is cut.
            lifetime.Listen(() => {
                if (_soundOrders[index] == order)
                    _soundSources[index].Stop();
            });
        }

        private int Play(Sound sound, in SoundPlay play)
        {
            var index = SelectSource(play.Limit);
            var source = _soundSources[index];

            source.Stop();
            _soundScales[index] = sound.Volume * play.Volume;
            _soundLimits[index] = play.Limit;
            _soundOrders[index] = ++_soundOrder;

            source.clip = sound.Clip;
            source.pitch = play.Pitch;
            source.panStereo = play.Pan;

            source.volume = _values[AudioLine.SFX] * _soundScales[index];

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

                for (var i = 0; i < _soundSources.Length; i++)
                {
                    if (_soundLimits[i] != limit || _soundSources[i].isPlaying == false)
                        continue;

                    count++;

                    if (oldest < 0 || _soundOrders[i] < _soundOrders[oldest])
                        oldest = i;
                }

                if (count >= limit.Max)
                    return oldest;
            }

            var first = 0;

            for (var i = 0; i < _soundSources.Length; i++)
            {
                if (_soundSources[i].isPlaying == false)
                    return i;

                if (_soundOrders[i] < _soundOrders[first])
                    first = i;
            }

            Debug.LogWarning($"[Audio] All {_soundSources.Length} sound sources play, the oldest sound is cut");
            return first;
        }

        public void PlayLoopMusic(Sound sound, float fadeIn)
        {
            var active = _music[_activeMusic];

            // The menu requests music on every entry: an already playing track is not restarted.
            if (active.IsPlaying(sound) == true)
                return;

            // The old track fades out over the same time the new one rises; one already fading keeps its pace.
            if (active.IsRising == true)
                active.FadeTo(0f, fadeIn);

            // The other source is free or still fades out an older track: that one is cut.
            _activeMusic = (_activeMusic + 1) % _music.Length;
            _music[_activeMusic].Start(sound, fadeIn);

            ApplyMusicVolume();
        }

        public void StopMusic(float fadeOut)
        {
            foreach (var channel in _music)
                channel.FadeTo(0f, fadeOut);

            ApplyMusicVolume();
        }

        // One music source with its fade: the volume of the source is line * clip * fade.
        private sealed class MusicChannel
        {
            public MusicChannel(AudioSource source)
            {
                Source = source;
            }

            private Sound _sound;
            private float _fade;
            private float _target;
            private float _speed;

            public AudioSource Source { get; }

            public bool IsRising => _sound != null && Mathf.Approximately(_target, 1f) == true;

            public bool IsPlaying(Sound sound)
            {
                return IsRising == true && Source.isPlaying == true && Source.clip == sound.Clip;
            }

            public void Start(Sound sound, float fadeIn)
            {
                _sound = sound;
                _fade = 0f;

                Source.Stop();
                Source.loop = true;
                Source.clip = sound.Clip;
                Source.Play();

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
                if (_sound == null || Mathf.Approximately(_fade, _target) == true)
                    return false;

                _fade = Mathf.MoveTowards(_fade, _target, _speed * delta);
                StopIfSilent();
                return true;
            }

            public void ApplyVolume(float line)
            {
                if (_sound == null)
                {
                    Source.volume = 0f;
                    return;
                }

                Source.volume = line * _sound.Volume * _fade;
            }

            private void StopIfSilent()
            {
                if (_fade > 0f || _target > 0f)
                    return;

                Source.Stop();
                Source.clip = null;
                _sound = null;
            }
        }
    }
}
