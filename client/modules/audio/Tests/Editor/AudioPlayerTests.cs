using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Audio.Tests
{
    // The fades of the music run on real time: the game paused (the updater speed is 0) does not freeze a crossfade
    // at its start - the music of the pause menu still rises.
    public class AudioPlayerTests
    {
        private static readonly AudioLine Dialogue = AudioLine.Of<Volume>("Dialogue", volume => volume.DialogueVolume);

        [UnityTest]
        public IEnumerator UpdaterPaused_MusicStillFadesIn() => UniTask.ToCoroutine(async () =>
        {
            var gameObject = new GameObject("audio");
            var music = new[] { gameObject.AddComponent<AudioSource>(), gameObject.AddComponent<AudioSource>() };
            var sounds = new[] { gameObject.AddComponent<AudioSource>() };
            var player = gameObject.AddComponent<AudioPlayer>();

            var serialized = new SerializedObject(player);
            Fill(serialized.FindProperty("_musicSources"), music);
            Fill(serialized.FindProperty("_soundSources"), sounds);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var updater = new FakeUpdater();
            var lifetime = new Lifetime();
            var clip = AudioClip.Create("track", 4410, 1, 44100, false);

            try
            {
                player.Construct(new Volume(), updater);
                player.OnSetupCompletion(lifetime);

                updater.Pause();
                player.PlayLoopMusic(new Sound(clip, 1f, null, "track"), 0.1f);

                for (var i = 0; i < 10; i++)
                {
                    await UniTask.Delay(30, DelayType.Realtime);
                    updater.Tick();
                }

                var volume = Mathf.Max(music[0].volume, music[1].volume);
                Assert.AreEqual(1f, volume, 0.001f, "the fade in stood still while the game was paused");
            }
            finally
            {
                lifetime.Terminate();
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        });

        // Two tracks in one frame: the first one has not risen from 0, it fades "to" 0 at once. Its source stops and gives
        // the voice back instead of playing silently for good.
        [Test]
        public void TwoTracksInOneFrame_FirstSourceIsReleased()
        {
            var gameObject = new GameObject("audio");
            var music = new[] { gameObject.AddComponent<AudioSource>(), gameObject.AddComponent<AudioSource>() };
            var sounds = new[] { gameObject.AddComponent<AudioSource>() };
            var player = gameObject.AddComponent<AudioPlayer>();

            var serialized = new SerializedObject(player);
            Fill(serialized.FindProperty("_musicSources"), music);
            Fill(serialized.FindProperty("_soundSources"), sounds);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var updater = new FakeUpdater();
            var lifetime = new Lifetime();
            var first = AudioClip.Create("first", 4410, 1, 44100, false);
            var second = AudioClip.Create("second", 4410, 1, 44100, false);

            try
            {
                player.Construct(new Volume(), updater);
                player.OnSetupCompletion(lifetime);

                player.PlayLoopMusic(new Sound(first, 1f, null, "first"), 0.1f);
                player.PlayLoopMusic(new Sound(second, 1f, null, "second"), 0.1f);

                updater.Tick();

                Assert.IsTrue(music[0].clip != first && music[1].clip != first, "the silent track keeps its source");
            }
            finally
            {
                lifetime.Terminate();
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void Setup_NotTwoMusicSources_Throws()
        {
            var gameObject = new GameObject("audio");
            var player = gameObject.AddComponent<AudioPlayer>();

            var serialized = new SerializedObject(player);
            Fill(serialized.FindProperty("_musicSources"), new[] { gameObject.AddComponent<AudioSource>() });
            Fill(serialized.FindProperty("_soundSources"), new AudioSource[0]);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var lifetime = new Lifetime();

            try
            {
                player.Construct(new Volume(), new FakeUpdater());
                Assert.Throws<InvalidOperationException>(() => player.OnSetupCompletion(lifetime));
            }
            finally
            {
                lifetime.Terminate();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Setup_SoundSourcesFollowSfxAndMaster()
        {
            using var rig = new Rig(2);

            rig.Volume.SetMaster(0.5f);
            rig.Volume.SetSounds(0.5f);

            Assert.AreEqual(0.25f, rig.Sounds[0].volume, 0.001f);
            Assert.AreEqual(0.25f, rig.Sounds[1].volume, 0.001f);
        }

        [Test]
        public void PlaySound_SetsTheSource()
        {
            using var rig = new Rig(1);

            rig.Volume.SetMaster(0.5f);
            rig.Volume.SetSounds(0.5f);

            rig.Player.PlaySound(rig.Sound("clip", 0.5f), new SoundPlay(0.5f, 1.5f, -0.5f, null));

            var source = rig.Sounds[0];
            Assert.AreEqual("clip", source.clip.name);
            Assert.AreEqual(1.5f, source.pitch, 0.001f);
            Assert.AreEqual(-0.5f, source.panStereo, 0.001f);
            Assert.AreEqual(0.0625f, source.volume, 0.001f);

            rig.Volume.SetMaster(1f);
            Assert.AreEqual(0.125f, source.volume, 0.001f, "a sound follows the volumes while it plays");
        }

        [Test]
        public void PlaySound_LineOfTheGame_FollowsItsVolumeFromTheFirstSound()
        {
            using var rig = new Rig(1);

            rig.Volume.SetDialogue(0.5f);
            rig.Player.PlaySound(rig.Sound("clip", 1f), SoundPlay.Default.WithLine(Dialogue));
            Assert.AreEqual(0.5f, rig.Sounds[0].volume, 0.001f);

            rig.Volume.SetDialogue(0.25f);
            Assert.AreEqual(0.25f, rig.Sounds[0].volume, 0.001f);

            rig.Volume.SetSounds(0.1f);
            Assert.AreEqual(0.25f, rig.Sounds[0].volume, 0.001f, "the line of the source is the one of its sound");

            rig.Player.PlaySound(rig.Sound("other", 1f));
            Assert.AreEqual(0.1f, rig.Sounds[0].volume, 0.001f, "the next sound goes back to SFX");
        }

        [Test]
        public void PlaySound_LineOfAnotherVolumeType_Throws()
        {
            using var rig = new Rig(1);

            var line = AudioLine.Of<OtherVolume>("Other", volume => volume.MasterVolume);

            Assert.Throws<InvalidOperationException>(
                () => rig.Player.PlaySound(rig.Sound("clip", 1f), SoundPlay.Default.WithLine(line)));
        }

        [Test]
        public void PlaySound_TrimLeavesNothing_Throws()
        {
            using var rig = new Rig(1);

            var sound = rig.Sound("clip", 1f);
            var length = sound.Clip.length;

            Assert.Throws<ArgumentException>(
                () => rig.Player.PlaySound(sound, SoundPlay.Default.WithTrim(length * 0.5f, length * 0.5f)));
        }

        [Test]
        public void PlaySound_TrimmedStart_SetsTheTime()
        {
            using var rig = new Rig(1);

            var sound = rig.Sound("clip", 1f);
            rig.Player.PlaySound(sound, SoundPlay.Default.WithTrim(0.25f, 0f));

            Assert.AreEqual(0.25f, rig.Sounds[0].time, 0.01f);
        }

        [Test]
        public void PlaySound_FreeSourceIsTaken()
        {
            using var rig = new Rig(2);

            rig.Player.PlaySound(rig.Sound("first", 1f));
            rig.Player.PlaySound(rig.Sound("second", 1f));

            Assert.AreEqual("first", rig.Sounds[0].clip.name);
            Assert.AreEqual("second", rig.Sounds[1].clip.name);
        }

        [Test]
        public void PlaySound_NoFreeSource_TheOldestIsCutWithAWarning()
        {
            using var rig = new Rig(2);

            rig.Player.PlaySound(rig.Sound("first", 1f));
            rig.Player.PlaySound(rig.Sound("second", 1f));

            LogAssert.Expect(LogType.Warning, "[Audio] All 2 sound sources play, the oldest sound is cut");
            rig.Player.PlaySound(rig.Sound("third", 1f));

            Assert.AreEqual("third", rig.Sounds[0].clip.name);
            Assert.AreEqual("second", rig.Sounds[1].clip.name);
        }

        [Test]
        public void PlaySound_OverTheLimit_TheOldestOfTheLimitIsCut()
        {
            using var rig = new Rig(4);

            var limit = new SoundLimit(2);
            var play = new SoundPlay(1f, 1f, 0f, limit);

            rig.Player.PlaySound(rig.Sound("free", 1f));
            rig.Player.PlaySound(rig.Sound("first", 1f), play);
            rig.Player.PlaySound(rig.Sound("second", 1f), play);

            rig.Player.PlaySound(rig.Sound("third", 1f), play);

            Assert.AreEqual("free", rig.Sounds[0].clip.name);
            Assert.AreEqual("third", rig.Sounds[1].clip.name);
            Assert.AreEqual("second", rig.Sounds[2].clip.name);
            Assert.IsTrue(rig.Sounds[3].clip == null);
        }

        [Test]
        public void PlaySound_LifetimeEnds_ThisSoundIsCut()
        {
            using var rig = new Rig(1);

            var soundLifetime = new Lifetime();
            rig.Player.PlaySound(rig.Sound("clip", 1f), SoundPlay.Default, soundLifetime);

            soundLifetime.Terminate();

            Assert.IsFalse(rig.Sounds[0].isPlaying);
        }

        [Test]
        public void PlaySound_LifetimeEnds_AnotherSoundOfTheSourceGoesOn()
        {
            using var rig = new Rig(1);

            var soundLifetime = new Lifetime();
            rig.Player.PlaySound(rig.Sound("clip", 1f), SoundPlay.Default, soundLifetime);

            LogAssert.Expect(LogType.Warning, "[Audio] All 1 sound sources play, the oldest sound is cut");
            rig.Player.PlaySound(rig.Sound("other", 1f));

            soundLifetime.Terminate();

            Assert.IsTrue(rig.Sounds[0].isPlaying);
        }

        [Test]
        public void PlaySound_TrimmedEnd_StopsThere()
        {
            using var rig = new Rig(1);

            var sound = rig.Sound("clip", 1f);
            var length = sound.Clip.length;

            // the end is before the start of the source: the first update cuts it
            rig.Player.PlaySound(sound, SoundPlay.Default.WithTrim(0.5f * length, 0.4f * length));

            rig.Sounds[0].time = 0.7f * length;
            rig.Updater.Tick();

            Assert.IsFalse(rig.Sounds[0].isPlaying);
        }

        [Test]
        public void Music_NoFade_LineAndClipVolumeAtOnce()
        {
            using var rig = new Rig(1);

            rig.Volume.SetMaster(0.5f);
            rig.Volume.SetMusic(0.5f);
            rig.Player.PlayLoopMusic(rig.Sound("track", 0.5f), 0f);

            var source = rig.ActiveMusic("track");
            Assert.IsTrue(source.loop);
            Assert.AreEqual(0.125f, source.volume, 0.001f);

            rig.Volume.SetMusic(1f);
            Assert.AreEqual(0.25f, source.volume, 0.001f);
        }

        [Test]
        public void Music_NotOnSoundSources_AndSfxDoesNotChangeIt()
        {
            using var rig = new Rig(1);

            rig.Player.PlayLoopMusic(rig.Sound("track", 1f), 0f);
            rig.Volume.SetSounds(0.1f);

            Assert.AreEqual(1f, rig.ActiveMusic("track").volume, 0.001f);
            Assert.IsTrue(rig.Sounds[0].clip == null);
        }

        [Test]
        public void Music_SourcesWithoutATrack_Silent()
        {
            using var rig = new Rig(1);

            rig.Volume.SetMaster(1f);

            Assert.AreEqual(0f, rig.Music[0].volume);
            Assert.AreEqual(0f, rig.Music[1].volume);
        }

        [Test]
        public void Music_FadeInStartsFromZero()
        {
            using var rig = new Rig(1);

            rig.Player.PlayLoopMusic(rig.Sound("track", 1f), 100f);

            Assert.AreEqual(0f, rig.ActiveMusic("track").volume, 0.001f);
        }

        [Test]
        public void Music_SameTrack_GoesOn()
        {
            using var rig = new Rig(1);

            var sound = rig.Sound("track", 1f);
            rig.Player.PlayLoopMusic(sound, 0f);

            var source = rig.ActiveMusic("track");
            rig.Player.PlayLoopMusic(sound, 0f);

            Assert.AreSame(source, rig.ActiveMusic("track"));
            Assert.IsTrue(source == rig.Music[1], "a request of the track that plays does not switch the source");
        }

        [Test]
        public void Music_OtherTrack_NoFade_OldOneStopsAtOnce()
        {
            using var rig = new Rig(1);

            rig.Player.PlayLoopMusic(rig.Sound("first", 1f), 0f);
            rig.Player.PlayLoopMusic(rig.Sound("second", 1f), 0f);

            Assert.AreEqual(1f, rig.ActiveMusic("second").volume, 0.001f);
            Assert.IsFalse(rig.HasMusic("first"));
        }

        [Test]
        public void Music_OtherTrack_OldOneFadesOverTheSameTime()
        {
            using var rig = new Rig(1);

            rig.Player.PlayLoopMusic(rig.Sound("first", 1f), 0f);
            rig.Player.PlayLoopMusic(rig.Sound("second", 1f), 100f);

            Assert.AreEqual(1f, rig.ActiveMusic("first").volume, 0.001f, "the old track starts its fade from where it is");
            Assert.AreEqual(0f, rig.ActiveMusic("second").volume, 0.001f);
        }

        [Test]
        public void StopMusic_AtOnce_FreesTheSources()
        {
            using var rig = new Rig(1);

            rig.Player.PlayLoopMusic(rig.Sound("track", 1f), 0f);
            rig.Player.StopMusic(0f);

            Assert.IsFalse(rig.HasMusic("track"));
            Assert.AreEqual(0f, rig.Music[0].volume);
            Assert.AreEqual(0f, rig.Music[1].volume);
        }

        [Test]
        public void StopMusic_WithFade_TrackStillThere()
        {
            using var rig = new Rig(1);

            rig.Player.PlayLoopMusic(rig.Sound("track", 1f), 0f);
            rig.Player.StopMusic(100f);

            Assert.IsTrue(rig.HasMusic("track"));
        }

        [Test]
        public void StopMusic_NothingPlays_NothingHappens()
        {
            using var rig = new Rig(1);

            rig.Player.StopMusic(0f);
            rig.Updater.Tick();

            Assert.IsFalse(rig.HasMusic("track"));
        }

        [Test]
        public void Music_LifetimeEnds_TrackFadesOut()
        {
            using var rig = new Rig(1);

            var musicLifetime = new Lifetime();
            rig.Player.PlayLoopMusic(rig.Sound("track", 1f), 0f, musicLifetime);

            musicLifetime.Terminate();

            Assert.IsFalse(rig.HasMusic("track"));
        }

        [Test]
        public void Music_LifetimeEnds_AnotherTrackGoesOn()
        {
            using var rig = new Rig(1);

            var musicLifetime = new Lifetime();
            rig.Player.PlayLoopMusic(rig.Sound("first", 1f), 0f, musicLifetime);
            rig.Player.PlayLoopMusic(rig.Sound("second", 1f), 0f);

            musicLifetime.Terminate();

            Assert.AreEqual(1f, rig.ActiveMusic("second").volume, 0.001f);
        }

        [Test]
        public void Music_LifetimeEnds_AfterTheSameTrackWasRequestedAgain_GoesOn()
        {
            using var rig = new Rig(1);

            var sound = rig.Sound("track", 1f);
            var musicLifetime = new Lifetime();
            rig.Player.PlayLoopMusic(sound, 0f, musicLifetime);

            rig.Player.PlayLoopMusic(sound, 0f);
            musicLifetime.Terminate();

            Assert.AreEqual(1f, rig.ActiveMusic("track").volume, 0.001f);
        }

        private static void Fill(SerializedProperty array, AudioSource[] sources)
        {
            array.arraySize = sources.Length;

            for (var i = 0; i < sources.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sources[i];
        }

        private class Volume : IAudioVolume
        {
            private readonly ViewableProperty<float> _master = new(1f);
            private readonly ViewableProperty<float> _sounds = new(1f);
            private readonly ViewableProperty<float> _music = new(1f);
            private readonly ViewableProperty<float> _dialogue = new(1f);

            public IViewableProperty<float> MasterVolume => _master;
            public IViewableProperty<float> SoundsVolume => _sounds;
            public IViewableProperty<float> MusicVolume => _music;
            public IViewableProperty<float> DialogueVolume => _dialogue;

            public void SetMaster(float value) => _master.Set(value);
            public void SetSounds(float value) => _sounds.Set(value);
            public void SetMusic(float value) => _music.Set(value);
            public void SetDialogue(float value) => _dialogue.Set(value);
        }

        private class OtherVolume : Volume
        {
        }

        // A player set up on its own object, with the clips of the test.
        private sealed class Rig : IDisposable
        {
            public Rig(int soundSources)
            {
                _gameObject = new GameObject("audio");
                Music = new[] { _gameObject.AddComponent<AudioSource>(), _gameObject.AddComponent<AudioSource>() };
                Sounds = new AudioSource[soundSources];

                for (var i = 0; i < soundSources; i++)
                    Sounds[i] = _gameObject.AddComponent<AudioSource>();

                Player = _gameObject.AddComponent<AudioPlayer>();

                var serialized = new SerializedObject(Player);
                Fill(serialized.FindProperty("_musicSources"), Music);
                Fill(serialized.FindProperty("_soundSources"), Sounds);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Player.Construct(Volume, Updater);
                Player.OnSetupCompletion(_lifetime);
            }

            private readonly GameObject _gameObject;
            private readonly Lifetime _lifetime = new();
            private readonly List<AudioClip> _clips = new();

            public AudioPlayer Player { get; }
            public AudioSource[] Music { get; }
            public AudioSource[] Sounds { get; }
            public Volume Volume { get; } = new();
            public FakeUpdater Updater { get; } = new();

            public Sound Sound(string name, float volume)
            {
                var clip = AudioClip.Create(name, 44100, 1, 44100, false);
                _clips.Add(clip);
                return new Sound(clip, volume, null, name);
            }

            public bool HasMusic(string track)
            {
                foreach (var source in Music)
                {
                    if (source.clip != null && source.clip.name == track)
                        return true;
                }

                return false;
            }

            public AudioSource ActiveMusic(string track)
            {
                foreach (var source in Music)
                {
                    if (source.clip != null && source.clip.name == track)
                        return source;
                }

                throw new InvalidOperationException($"No music source has {track}");
            }

            public void Dispose()
            {
                _lifetime.Terminate();
                UnityEngine.Object.DestroyImmediate(_gameObject);

                foreach (var clip in _clips)
                    UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        // The updater of the scope: the delta is the unscaled time by the speed, 0 while paused.
        private class FakeUpdater : IUpdater
        {
            private readonly ViewableProperty<float> _speed = new(1f);
            private readonly List<IUpdatable> _updatables = new();

            public IViewableProperty<float> Speed => _speed;

            public void Tick()
            {
                foreach (var updatable in _updatables)
                    updatable.OnUpdate(0.03f * _speed.Value);
            }

            public void SetSpeed(float speed) => _speed.Set(speed);
            public void Pause() => _speed.Set(0f);
            public void Continue() => _speed.Set(1f);

            public void Add(IReadOnlyLifetime l, IUpdatable u) => _updatables.Add(u);
            public void Add(IReadOnlyLifetime l, IPreUpdatable updatable) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IPostFixedUpdatable updatable) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IGizmosUpdatable updatable) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IPostUpdatable updatable) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, ILateUpdatable updatable) => throw new NotSupportedException();
        }
    }
}
