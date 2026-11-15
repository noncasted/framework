using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    public class AssetGroupTests
    {
        [Test]
        public void Release_WhileLoading_UnloadsWhenTheLoadCompletes()
        {
            var group = new TestGroup();

            var retain = group.Retain();
            group.Release();

            CollectionAssert.AreEqual(new[] { "load" }, group.Log);

            group.CompleteLoad();

            CollectionAssert.AreEqual(new[] { "load", "loaded", "unload" }, group.Log);
            Assert.IsFalse(group.IsLoaded);
            Assert.AreEqual(UniTaskStatus.Succeeded, retain.Status);
        }

        [Test]
        public void ReleaseAndRetain_WhileLoading_KeepTheSameLoad()
        {
            var group = new TestGroup();

            group.Retain().NoAwait();
            group.Release();
            var retain = group.Retain();

            group.CompleteLoad();

            CollectionAssert.AreEqual(new[] { "load", "loaded" }, group.Log);
            Assert.IsTrue(group.IsLoaded);
            Assert.AreEqual(UniTaskStatus.Succeeded, retain.Status);

            group.Release();

            CollectionAssert.AreEqual(new[] { "load", "loaded", "unload" }, group.Log);
        }

        [Test]
        public void FailedLoad_IsUnloaded_TheNextRetainLoadsAgain()
        {
            var group = new TestGroup();

            var failed = group.Retain();
            group.FailLoad();

            Assert.AreEqual(UniTaskStatus.Faulted, failed.Status);
            Assert.Throws<InvalidOperationException>(() => failed.GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "load", "unload" }, group.Log);
            Assert.IsFalse(group.IsLoaded);

            // The failed retain is still a retain: its owner releases it.
            group.Release();

            var retry = group.Retain();
            group.CompleteLoad();

            Assert.AreEqual(UniTaskStatus.Succeeded, retry.Status);
            Assert.IsTrue(group.IsLoaded);

            group.Release();

            Assert.IsFalse(group.IsLoaded);
        }

        // The unload of a failed load throws: the awaiters of Retain still get the error of the load.
        [Test]
        public void FailedLoad_UnloadThrows_RetainFails()
        {
            var group = new TestGroup();
            group.FailUnload();
            LogAssert.Expect(LogType.Exception, new Regex("unload failed"));

            var retain = group.Retain();
            group.FailLoad();

            Assert.AreEqual(UniTaskStatus.Faulted, retain.Status, "Retain waits for a load that is over");
            Assert.Throws<InvalidOperationException>(() => retain.GetAwaiter().GetResult());
            group.Release();
        }

        // Released while loading, the unload throws: the awaiters of Retain are not left waiting.
        [Test]
        public void ReleasedWhileLoading_UnloadThrows_RetainCompletes()
        {
            var group = new TestGroup();
            group.FailUnload();
            LogAssert.Expect(LogType.Exception, new Regex("unload failed"));

            var retain = group.Retain();
            group.Release();
            group.CompleteLoad();

            Assert.AreEqual(UniTaskStatus.Succeeded, retain.Status, "Retain waits for a load that is over");
        }

        [Test]
        public void Release_WithoutRetain_Throws()
        {
            var group = new TestGroup();

            Assert.Throws<InvalidOperationException>(() => group.Release());
        }

        [UnityTest]
        public IEnumerator LoadAssetGroup_FailedLoad_ReleasedWithTheScope() => UniTask.ToCoroutine(async () =>
        {
            var group = new TestGroup();
            var builder = new TestScopeBuilder();

            var load = builder.LoadAssetGroup(group, "Test");
            group.FailLoad();

            var isFailed = false;

            try
            {
                await load;
            }
            catch (InvalidOperationException)
            {
                isFailed = true;
            }

            Assert.IsTrue(isFailed);

            await builder.Events.InvokeAfterDispose();

            // Nobody holds the group now: one retain and one release load and unload it.
            group.Log.Clear();
            var retain = group.Retain();
            group.CompleteLoad();
            await retain;
            group.Release();

            CollectionAssert.AreEqual(new[] { "load", "loaded", "unload" }, group.Log);
        });

        internal sealed class TestGroup : AssetGroup
        {
            private UniTaskCompletionSource _pending;
            private bool _failsToUnload;

            public readonly List<string> Log = new();

            public void FailUnload()
            {
                _failsToUnload = true;
            }

            public void CompleteLoad()
            {
                _pending.TrySetResult();
            }

            public void FailLoad()
            {
                _pending.TrySetException(new InvalidOperationException("The load failed"));
            }

            protected override async UniTask LoadGroup()
            {
                Log.Add("load");
                _pending = new UniTaskCompletionSource();
                await _pending.Task;
                Log.Add("loaded");
            }

            protected override void UnloadGroup()
            {
                Log.Add("unload");

                if (_failsToUnload == true)
                    throw new InvalidOperationException("unload failed");
            }
        }

        private sealed class TestScopeBuilder : IScopeBuilder
        {
            private readonly Lifetime _lifetime = new();

            public IContainerRegistry Registry => throw new NotSupportedException();
            public IEventLoop Events { get; } = new EventLoop();
            public IReadOnlyLifetime Lifetime => _lifetime;
            public ServiceScopeSceneLoader SceneLoader => throw new NotSupportedException();
            public IServiceScopeBinder Binder => throw new NotSupportedException();
            public ILifetime ScopeLifetime => _lifetime;
            public bool IsMock => false;
        }
    }
}
