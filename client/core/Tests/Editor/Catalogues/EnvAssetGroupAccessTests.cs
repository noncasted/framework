using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.Tests
{
    // The runtime access to the whole group of one asset type: a lookup built from every asset of a kind (node styles by
    // key) instead of the generated properties.
    public class EnvAssetGroupAccessTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(_created[i]);

            _created.Clear();
        }

        [Test]
        public void GetAll_ReturnsOnlyTheRequestedType()
        {
            var first = Create<FirstAsset>();
            var second = Create<SecondAsset>();
            var third = Create<FirstAsset>();
            var group = CreateGroup(first, second, third);

            CollectionAssert.AreEqual(new EnvAsset[] { first, third }, group.GetAll<FirstAsset>());
        }

        [Test]
        public void GetAll_EmptyEntry_Throws()
        {
            var group = CreateGroup(Create<FirstAsset>(), null);

            Assert.Throws<InvalidOperationException>(() => group.GetAll<FirstAsset>());
        }

        private EnvAssetGroupAsset CreateGroup(params EnvAsset[] assets)
        {
            var group = ScriptableObject.CreateInstance<EnvAssetGroupAsset>();
            _created.Add(group);

            var serializedObject = new SerializedObject(group);
            var entries = serializedObject.FindProperty("_entries");
            entries.arraySize = assets.Length;

            for (var i = 0; i < assets.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative(EnvAssetEntry.NameKey).stringValue = i.ToString();
                entry.FindPropertyRelative(EnvAssetEntry.AssetKey).objectReferenceValue = assets[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return group;
        }

        private T Create<T>() where T : EnvAsset
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        private sealed class FirstAsset : EnvAsset
        {
        }

        private sealed class SecondAsset : EnvAsset
        {
        }
    }
}
