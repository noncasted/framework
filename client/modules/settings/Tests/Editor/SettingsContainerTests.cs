using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.Publisher;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Settings.Tests
{
    // The saves are loaded by AddSaves while the scope is built and the settings read them when they are built: every
    // service that takes the settings sees the saved values in its OnSetup, whatever its place among the registrations.
    public class SettingsContainerTests
    {
        [UnityTest]
        public IEnumerator Setup_ListenerBeforeTheSettings_SeesTheSavedVolume() => UniTask.ToCoroutine(async () =>
        {
            SettingsContainerProbe.Reset(0.25f);
            GeneratedRoots.Register(((System.Func<IScopeBuilder, UniTask>)SettingsContainerRoots.ListenerFirst).Method);

            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, SettingsContainerRoots.ListenerFirst));

            try
            {
                Assert.AreEqual(0.25f, SettingsContainerProbe.SeenVolume);
            }
            finally
            {
                await scope.Dispose();
                Object.DestroyImmediate(SettingsContainerProbe.Options);
            }
        });
    }

    public static class SettingsContainerProbe
    {
        public static SettingsOptions Options { get; private set; }
        public static ISaves Saves { get; private set; }
        public static float SeenVolume { get; private set; }

        public static void Reset(float savedVolume)
        {
            // A save the player applied: JsonUtility writes the fields as their backing fields.
            var save = JsonUtility.FromJson<SettingsSave>(
                "{\"<MasterVolume>k__BackingField\":" + savedVolume.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"<WasChanged>k__BackingField\":true}");

            Options = ScriptableObject.CreateInstance<SettingsOptions>();
            Saves = new StoredSaves(save);
            SeenVolume = -1f;
        }

        public static void See(float volume)
        {
            SeenVolume = volume;
        }

        private class StoredSaves : ISaves
        {
            public StoredSaves(SettingsSave save)
            {
                _entries[typeof(SettingsSave)] = save;
            }

            private readonly Dictionary<System.Type, object> _entries = new();

            private bool _isLoaded;

            public UniTask Load()
            {
                _isLoaded = true;
                return UniTask.CompletedTask;
            }

            // AddSaves loads them before the settings are built.
            public T Get<T>() where T : class, new()
            {
                if (_isLoaded == false)
                    throw new System.InvalidOperationException("Read before Load");

                return _entries.TryGetValue(typeof(T), out var entry) == true ? (T)entry : new T();
            }

            public UniTask Save<T>(T data)
            {
                _entries[typeof(T)] = data;
                return UniTask.CompletedTask;
            }
        }
    }

    // Reads the volume in its own setup: registered before the settings, so it is set up before them.
    public class VolumeListener : IScopeSetup
    {
        public VolumeListener(ISettings<SettingsSave> settings)
        {
            _settings = settings;
        }

        private readonly ISettings<SettingsSave> _settings;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            SettingsContainerProbe.See(_settings.MasterVolume.Value);
        }
    }

    public static class SettingsContainerRoots
    {
        [ContainerGraphRoot]
        public static async UniTask ListenerFirst(IScopeBuilder builder)
        {
            builder.Register<VolumeListener>().As<IScopeSetup>();
            await builder.AddSaves(SettingsContainerProbe.Saves);
            builder.AddSettings(SettingsContainerProbe.Options);
        }
    }
}
