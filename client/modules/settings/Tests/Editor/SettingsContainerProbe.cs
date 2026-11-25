using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.Publisher;
using UnityEngine;

namespace Global.Settings.Tests
{
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
}
