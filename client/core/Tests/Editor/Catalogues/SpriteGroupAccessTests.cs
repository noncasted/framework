using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.Tests
{
    // The runtime access to a sprite group: content that names its sprites in data (folder icons by index) and content
    // that walks the whole group (random backgrounds) goes through the loaded group asset.
    public class SpriteGroupAccessTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
                UnityEngine.Object.DestroyImmediate(created);

            _created.Clear();
        }

        [Test]
        public void Sheets_ReturnsOnlySheetsSortedByName()
        {
            var first = CreateSprite();
            var second = CreateSprite();
            var animation = CreateSprite();
            var group = CreateGroup(
                ("Beta", SpriteKind.Sheet, second),
                ("Animation", SpriteKind.Animation, animation),
                ("Alpha", SpriteKind.Sheet, first));

            CollectionAssert.AreEqual(new[] { first, second }, group.Sheets);
        }

        [Test]
        public void GetSheet_ReturnsTheNamedSheet()
        {
            var sheet = CreateSprite();
            var group = CreateGroup(("Icon_000", SpriteKind.Sheet, sheet));

            Assert.AreSame(sheet, group.GetSheet("Icon_000"));
        }

        [Test]
        public void GetSheet_Missing_Throws()
        {
            var group = CreateGroup(("Icon_000", SpriteKind.Sheet, CreateSprite()));

            Assert.Throws<InvalidOperationException>(() => group.GetSheet("Icon_001"));
        }

        [Test]
        public void GetSheet_AnimationName_Throws()
        {
            var group = CreateGroup(("Run", SpriteKind.Animation, CreateSprite()));

            Assert.Throws<InvalidOperationException>(() => group.GetSheet("Run"));
        }

        [Test]
        public void GroupAccess_BeforeLoad_Throws()
        {
            var group = new TestSpriteGroup();

            Assert.Throws<InvalidOperationException>(() => group.GetSheet("Icon_000"));
            Assert.Throws<InvalidOperationException>(() => _ = group.Sheets);
        }

        private SpriteGroupAsset CreateGroup(params (string Name, SpriteKind Kind, Sprite Sprite)[] entries)
        {
            var asset = ScriptableObject.CreateInstance<SpriteGroupAsset>();
            _created.Add(asset);

            var serializedObject = new SerializedObject(asset);
            var list = serializedObject.FindProperty("_entries");
            list.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative(SpriteEntry.NameKey).stringValue = entries[i].Name;
                entry.FindPropertyRelative(SpriteEntry.KindKey).enumValueIndex = (int)entries[i].Kind;

                var sprites = entry.FindPropertyRelative(SpriteEntry.SpritesKey);
                sprites.arraySize = 1;
                sprites.GetArrayElementAtIndex(0).objectReferenceValue = entries[i].Sprite;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(1, 1);
            _created.Add(texture);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
            _created.Add(sprite);
            return sprite;
        }

        private sealed class TestSpriteGroup : SpriteGroup
        {
            protected override UniTask LoadGroup()
            {
                return UniTask.CompletedTask;
            }

            protected override void UnloadGroup()
            {
            }
        }
    }
}
