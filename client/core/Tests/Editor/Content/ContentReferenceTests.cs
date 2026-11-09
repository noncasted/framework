using System;
using NUnit.Framework;
using Unity.Loading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    // An empty content reference or scene is an error of the caller: every call of the loader throws at once, before any
    // fetch or load; the target of a reference that is not loaded throws instead of giving null.
    public class ContentReferenceTests
    {
        private TextAsset _root;
        private ContentLoader _loader;
        private Lifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _root = new TextAsset("root");
            _loader = new ContentLoader(new EditorContentDirectory("Tests", new Object[] { _root }));
            _lifetime = new Lifetime();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void EmptyReference_EveryCallThrows()
        {
            var reference = new ContentReference<TextAsset>(default, string.Empty, 0);

            Assert.IsTrue(reference.IsEmpty);
            Assert.Throws<ArgumentException>(() => _loader.Load(_lifetime, reference));
            Assert.Throws<ArgumentException>(() => _loader.Load(_lifetime, new[] { reference }).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => _loader.LoadNow(_lifetime, reference));
            Assert.Throws<ArgumentException>(() => _loader.Fetch(_lifetime, reference));
            Assert.Throws<ArgumentException>(() => _loader.Progress(reference));
        }

        [Test]
        public void EmptyScene_EveryCallThrows()
        {
            var scene = new ContentScene(default, string.Empty);

            Assert.IsTrue(scene.IsEmpty);
            Assert.Throws<ArgumentException>(() => _loader.LoadScene(_lifetime, scene));
            Assert.Throws<ArgumentException>(() => _loader.Fetch(_lifetime, scene));
            Assert.Throws<ArgumentException>(() => _loader.Progress(scene));
        }

        [Test]
        public void TargetOfAReferenceNotLoaded_Throws()
        {
            var reference = new ContentReference<TextAsset>(default, string.Empty, 0);

            Assert.IsFalse(reference.IsLoaded);
            Assert.Throws<InvalidOperationException>(() => _ = reference.Target);
        }

        [Test]
        public void EndedLifetime_Throws()
        {
            var ended = new Lifetime();
            ended.Terminate();
            var reference = new ContentReference<TextAsset>(ValidId(), "0123456789abcdef0123456789abcdef", 11400000);

            Assert.Throws<OperationCanceledException>(() => _loader.Load(ended, reference));
            Assert.IsFalse(reference.IsLoaded);
        }

        private static LoadableObjectId ValidId()
        {
            return UnityEditor.LoadableObjectIdEditorUtility.CreateLoadableObjectId(
                new GUID("0123456789abcdef0123456789abcdef"), FileIdentifierType.SourceAsset, 11400000);
        }
    }
}
