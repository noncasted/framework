using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global.Localization
{
    // The texts of the table file by key, a column per language. The file is a list of entries
    //     { "key": "Menu.Play", "ru": "<the Russian text>", "en": "PLAY" }
    // (JsonUtility: no dictionaries). A key twice, an entry without a key or without the text of a language fails
    // the load: a text missing in one language is a bug, not an empty label.
    public class LocalizationTable
    {
        public LocalizationTable(string json)
        {
            var file = JsonUtility.FromJson<TableFile>(json);

            if (file?.entries == null)
                throw new InvalidOperationException("[Localization] The table has no entries");

            foreach (var entry in file.entries)
            {
                if (string.IsNullOrEmpty(entry.key) == true)
                    throw new InvalidOperationException("[Localization] An entry without a key");

                if (string.IsNullOrEmpty(entry.ru) == true || string.IsNullOrEmpty(entry.en) == true)
                    throw new InvalidOperationException($"[Localization] {entry.key}: a language has no text");

                if (_texts.ContainsKey(entry.key) == true)
                    throw new InvalidOperationException($"[Localization] {entry.key} is in the table twice");

                // Indexed by Language: the order of the columns is the order of the enum.
                _texts.Add(entry.key, new[] { entry.ru, entry.en });
            }
        }

        private readonly Dictionary<string, string[]> _texts = new();

        public IEnumerable<string> Keys => _texts.Keys;

        public bool Contains(TextKey key)
        {
            return _texts.ContainsKey(key.Id);
        }

        public string Get(TextKey key, Language language)
        {
            if (_texts.TryGetValue(key.Id, out var texts) == false)
                throw new KeyNotFoundException($"[Localization] No text for {key.Id}");

            return texts[(int)language];
        }

        [Serializable]
        private class TableFile
        {
            public List<TableEntry> entries;
        }

        [Serializable]
        private class TableEntry
        {
            public string key;
            public string ru;
            public string en;
        }
    }
}
