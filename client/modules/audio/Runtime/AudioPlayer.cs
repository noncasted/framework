using Internal;
using UnityEngine;

namespace Global.Audio
{
    [DisallowMultipleComponent]
    public class AudioPlayer : MonoBehaviour, IAudioPlayer, IScopeSetup, IUpdatable
    {
        [SerializeField] private AudioSource[] _musicSources;
        [SerializeField] private AudioSource[] _soundSources;

        private IUpdater _updater;
        private AudioLineVolumes _volumes;
        private MusicPlayer _music;
        private SoundPool _sounds;
        private double _lastTime;

        // Ready to play once injected, so a service may play from its own setup: the settings that implement IAudioVolume
        // have read their save when they were built, and the sliders change the volume on the fly.
        [Inject]
        public void Construct(IAudioVolume volume, IUpdater updater, IReadOnlyLifetime lifetime)
        {
            _updater = updater;

            _volumes = new AudioLineVolumes(volume, lifetime, ApplyVolume);
            _music = new MusicPlayer(_musicSources, _volumes);
            _sounds = new SoundPool(_soundSources, _volumes);

            _volumes.Start();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _lastTime = Time.realtimeSinceStartupAsDouble;
            _updater.Add(lifetime, this);
        }

        // The delta of the updater is the game time (0 while the game is paused): the music fades on real time, so
        // a crossfade into the music of the pause menu does not stand still.
        public void OnUpdate(float delta)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var realDelta = (float)(now - _lastTime);
            _lastTime = now;

            _music.Step(realDelta);
            _sounds.CutTrimmed();
        }

        public void PlaySound(Sound sound)
        {
            _sounds.Play(sound, SoundPlay.Default);
        }

        public void PlaySound(Sound sound, in SoundPlay play)
        {
            _sounds.Play(sound, play);
        }

        public void PlaySound(Sound sound, in SoundPlay play, IReadOnlyLifetime lifetime)
        {
            _sounds.Play(sound, play, lifetime);
        }

        public void PlayLoopMusic(Sound sound, float fadeIn)
        {
            _music.Play(sound, fadeIn);
        }

        public void PlayLoopMusic(Sound sound, float fadeIn, IReadOnlyLifetime lifetime)
        {
            _music.Play(sound, fadeIn, lifetime);
        }

        public void StopMusic(float fadeOut)
        {
            _music.Stop(fadeOut);
        }

        private void ApplyVolume()
        {
            _music.ApplyVolume();
            _sounds.ApplyVolume();
        }
    }
}
