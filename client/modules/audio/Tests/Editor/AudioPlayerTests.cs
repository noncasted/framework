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

        private static void Fill(SerializedProperty array, AudioSource[] sources)
        {
            array.arraySize = sources.Length;

            for (var i = 0; i < sources.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sources[i];
        }

        private class Volume : IAudioVolume
        {
            public IViewableProperty<float> MasterVolume { get; } = new ViewableProperty<float>(1f);
            public IViewableProperty<float> SoundsVolume { get; } = new ViewableProperty<float>(1f);
            public IViewableProperty<float> MusicVolume { get; } = new ViewableProperty<float>(1f);
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
