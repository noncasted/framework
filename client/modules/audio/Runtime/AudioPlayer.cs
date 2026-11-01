using Internal;
using UnityEngine;

namespace Global.Audio
{
    [DisallowMultipleComponent]
    public class AudioPlayer : MonoBehaviour, IAudioPlayer, IScopeSetupCompletion, IUpdatable
    {
        [SerializeField] private AudioSource[] _musicSources;
        [SerializeField] private AudioSource[] _soundSources;

        private IAudioVolume _volume;
        private IUpdater _updater;
        private AudioLineVolumes _volumes;
        private MusicPlayer _music;
        private SoundPool _sounds;
        private double _lastTime;

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
            _volumes = new AudioLineVolumes(_volume, lifetime, ApplyVolume);
            _music = new MusicPlayer(_musicSources, _volumes);
            _sounds = new SoundPool(_soundSources, _volumes);

            _volumes.Start();

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
