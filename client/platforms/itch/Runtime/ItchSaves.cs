using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Global.Publisher.Itch
{
    public class ItchSaves : ISaves, IScopeBaseSetup
    {
        // JsonUtility writes [field: SerializeField] as "<Name>k__BackingField", so saves under
        // the old key (written by Newtonsoft) are not readable: they are deleted, the settings are reset once.
        private const string Key = "save_v2";
        private const string LegacyKey = "save";

        private readonly Dictionary<string, string> _entries = new();

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            if (PlayerPrefs.HasKey(LegacyKey) == true)
                PlayerPrefs.DeleteKey(LegacyKey);

            if (PlayerPrefs.HasKey(Key) == false)
                return;

            // Only Save writes the key: a value that does not parse or an entry without a key is a broken save.
            var container = Parse<SaveEntries>(Key, PlayerPrefs.GetString(Key));

            if (container.Entries == null)
                throw new InvalidOperationException($"[ItchSaves] {Key} has no entries list");

            foreach (var entry in container.Entries)
            {
                if (string.IsNullOrEmpty(entry.Key) == true)
                    throw new InvalidOperationException($"[ItchSaves] {Key} has an entry without a key");

                _entries[entry.Key] = entry.Value;
            }
        }

        public T Get<T>() where T : class, new()
        {
            var key = typeof(T).FullName!;

            if (_entries.TryGetValue(key, out var rawEntry) == false)
                return new T();

            return Parse<T>(key, rawEntry);
        }

        public UniTask Save<T>(T data)
        {
            var key = typeof(T).FullName!;
            _entries[key] = JsonUtility.ToJson(data);

            var container = new SaveEntries();

            foreach (var (entryKey, value) in _entries)
                container.Entries.Add(new SaveEntry { Key = entryKey, Value = value });

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(container));

            // F2.25: on the web an unflushed value is lost with the tab.
            PlayerPrefs.Save();

            return UniTask.CompletedTask;
        }

        // JsonUtility throws on malformed JSON and returns null for an empty one: both fail the load.
        private static T Parse<T>(string key, string json) where T : class
        {
            if (string.IsNullOrEmpty(json) == true)
                throw new InvalidOperationException($"[ItchSaves] {key}: the saved value is empty");

            var value = JsonUtility.FromJson<T>(json);

            if (value == null)
                throw new InvalidOperationException($"[ItchSaves] {key}: the saved value is not a {typeof(T).Name}");

            return value;
        }

        [Serializable]
        private class SaveEntries
        {
            public List<SaveEntry> Entries = new();
        }

        [Serializable]
        private class SaveEntry
        {
            public string Key;
            public string Value;
        }
    }
}
