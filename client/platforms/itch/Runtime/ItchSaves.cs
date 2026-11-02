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

        // The last broken save (OnBaseSetup), for a report.
        private const string CorruptKey = "save_v2_corrupt";

        private readonly Dictionary<string, string> _entries = new();

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            if (PlayerPrefs.HasKey(LegacyKey) == true)
                PlayerPrefs.DeleteKey(LegacyKey);

            if (PlayerPrefs.HasKey(Key) == false)
                return;

            var raw = PlayerPrefs.GetString(Key);

            try
            {
                Load(raw);
            }
            catch (Exception exception)
            {
                // The start fails loudly once; left in place the same blob failed every next start, and the player
                // could not get past it. The blob is kept for the report, the next start begins with empty saves.
                _entries.Clear();
                PlayerPrefs.SetString(CorruptKey, raw);
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();

                throw new InvalidOperationException(
                    $"[ItchSaves] {Key} is broken, kept as {CorruptKey}: the saves start empty from the next start",
                    exception);
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
            if (data == null)
                throw new ArgumentNullException(nameof(data), $"[ItchSaves] A null {typeof(T).Name} is saved");

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

        // Only Save writes the key: a value that does not parse or an entry without a key is a broken save.
        private void Load(string raw)
        {
            var container = Parse<SaveEntries>(Key, raw);

            if (container.Entries == null)
                throw new InvalidOperationException($"[ItchSaves] {Key} has no entries list");

            foreach (var entry in container.Entries)
            {
                if (string.IsNullOrEmpty(entry.Key) == true)
                    throw new InvalidOperationException($"[ItchSaves] {Key} has an entry without a key");

                // The type of the value is not known here: it has to be a JSON object at least, as Save writes it.
                Parse<SaveValue>(entry.Key, entry.Value);

                _entries[entry.Key] = entry.Value;
            }
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
        private class SaveValue
        {
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
