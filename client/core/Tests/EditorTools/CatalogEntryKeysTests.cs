using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.EditorTests
{
    // The generators and the color catalog editor write the records through their serialized keys. A wrong key gives
    // a null property at run time, so every key of every record is checked to reach the field its getter reads.
    public class CatalogEntryKeysTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created)
                Object.DestroyImmediate(target);

            _created.Clear();
        }

        [Test]
        public void EnvAssetEntry_Keys_ReachTheFields()
        {
            var asset = Create<EnvAssetGroupAsset>();
            var target = Create<OptionsContainer>();
            var serialized = new SerializedObject(asset);
            var entry = AddElement(serialized.FindProperty("_entries"));

            Key(entry, EnvAssetEntry.NameKey).stringValue = "First";
            Key(entry, EnvAssetEntry.AssetKey).objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var read = First<EnvAssetEntry>(asset);
            Assert.AreEqual("First", read.Name);
            Assert.AreEqual(target, read.Asset);
        }

        [Test]
        public void AudioAssetEntry_Keys_ReachTheFields()
        {
            var asset = Create<AudioGroupAsset>();
            var clip = Track(AudioClip.Create("Clip", 100, 1, 8000, false));
            var encoded = Track(new TextAsset("Encoded"));
            var serialized = new SerializedObject(asset);
            var entry = AddElement(serialized.FindProperty("_entries"));

            Key(entry, AudioAssetEntry.NameKey).stringValue = "First";
            Key(entry, AudioAssetEntry.ClipKey).objectReferenceValue = clip;
            Key(entry, AudioAssetEntry.EncodedKey).objectReferenceValue = encoded;
            Key(entry, AudioAssetEntry.VolumeKey).floatValue = 0.25f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var read = First<AudioAssetEntry>(asset);
            Assert.AreEqual("First", read.Name);
            Assert.AreEqual(clip, read.Clip);
            Assert.AreEqual(encoded, read.Encoded);
            Assert.AreEqual(0.25f, read.Volume);
        }

        [Test]
        public void ColorRecords_Keys_ReachTheFields()
        {
            var asset = Create<ColorCatalog>();
            var serialized = new SerializedObject(asset);
            var group = AddElement(serialized.FindProperty("_groups"));

            Key(group, ColorGroup.NameKey).stringValue = "game";

            var entry = AddElement(Key(group, ColorGroup.EntriesKey));
            Key(entry, ColorEntry.NameKey).stringValue = "text";
            Key(entry, ColorEntry.ValueKey).colorValue = Color.red;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("game", asset.Groups[0].Name);
            Assert.AreEqual("text", asset.Groups[0].Entries[0].Name);
            Assert.AreEqual(Color.red, asset.Groups[0].Entries[0].Value);
        }

        [Test]
        public void PrefabAssetEntry_Keys_ReachTheFields()
        {
            var asset = Create<PrefabGroupAsset>();
            var prefab = Track(new GameObject("Prefab"));
            var serialized = new SerializedObject(asset);
            var entry = AddElement(serialized.FindProperty("_entries"));

            Key(entry, PrefabAssetEntry.NameKey).stringValue = "First";
            Key(entry, PrefabAssetEntry.PrefabKey).objectReferenceValue = prefab;
            Key(entry, PrefabAssetEntry.ComponentTypeKey).stringValue = "Component";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var read = First<PrefabAssetEntry>(asset);
            Assert.AreEqual("First", read.Name);
            Assert.AreEqual(prefab, read.Prefab);
            Assert.AreEqual("Component", read.ComponentType);
        }

        [Test]
        public void SpriteEntry_Keys_ReachTheFields()
        {
            var asset = Create<SpriteGroupAsset>();
            var texture = Track(new Texture2D(2, 2));
            var sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero));
            var serialized = new SerializedObject(asset);
            var entry = AddElement(serialized.FindProperty("_entries"));

            Key(entry, SpriteEntry.NameKey).stringValue = "First";
            Key(entry, SpriteEntry.KindKey).enumValueIndex = (int)SpriteKind.Animation;
            AddElement(Key(entry, SpriteEntry.SpritesKey)).objectReferenceValue = sprite;
            Key(entry, SpriteEntry.TimeKey).floatValue = 0.25f;
            Key(entry, SpriteEntry.ColorKey).colorValue = Color.red;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var read = First<SpriteEntry>(asset);
            Assert.AreEqual("First", read.Name);
            Assert.AreEqual(SpriteKind.Animation, read.Kind);
            Assert.AreEqual(sprite, read.Sprites[0]);
            Assert.AreEqual(0.25f, read.Time);
            Assert.AreEqual(Color.red, read.Color);
        }

        private static SerializedProperty Key(SerializedProperty parent, string key)
        {
            var property = parent.FindPropertyRelative(key);
            Assert.IsNotNull(property, $"{parent.type} has no key {key}");
            return property;
        }

        private static SerializedProperty AddElement(SerializedProperty array)
        {
            array.arraySize = 1;
            return array.GetArrayElementAtIndex(0);
        }

        private static TEntry First<TEntry>(ScriptableObject asset)
        {
            var field = asset.GetType().GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            return ((TEntry[])field.GetValue(asset))[0];
        }

        private T Create<T>() where T : ScriptableObject
        {
            return Track(ScriptableObject.CreateInstance<T>());
        }

        private T Track<T>(T target) where T : Object
        {
            _created.Add(target);
            return target;
        }
    }
}
