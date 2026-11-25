using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The asset groups a scope asks for: a group is released only by the scope that has retained it, and one failing
    // release does not stop the release of the others.
    [Category("Scopes")]
    public class AssetGroupRequestTests
    {
        [SetUp]
        public void SetUp()
        {
            ScopeLoaderProbe.Reset();
            GeneratedRoots.Register(((Func<IScopeBuilder, UniTask>)AssetGroupRequestRoots.RequestThenFail).Method);
            GeneratedRoots.Register(((Func<IScopeBuilder, UniTask>)AssetGroupRequestRoots.LoadTwo).Method);
        }

        // Construct fails after RequestAssetGroup: the build events have not run, nothing was retained by this scope.
        [UnityTest]
        public IEnumerator ConstructFailsAfterRequest_GroupOfAnotherOwnerStaysLoaded() => UniTask.ToCoroutine(async () =>
        {
            var group = new ProbeGroup();
            AssetGroupRequestRoots.SetFirst(group);
            await group.Retain();

            var caught = await Catch(() => new ServiceScopeLoader().Load(
                new ScopeLoadOptions(null, AssetGroupRequestRoots.RequestThenFail)));

            Assert.AreEqual("construct failed", caught.Message);
            Assert.IsTrue(group.IsLoaded, "the group was unloaded under its living owner");
        });

        [UnityTest]
        public IEnumerator ConstructFailsAfterRequest_NoOtherOwner_NoReleaseWithoutRetain() => UniTask.ToCoroutine(async () =>
        {
            AssetGroupRequestRoots.SetFirst(new ProbeGroup());

            var caught = await Catch(() => new ServiceScopeLoader().Load(
                new ScopeLoadOptions(null, AssetGroupRequestRoots.RequestThenFail)));

            Assert.AreEqual("construct failed", caught.Message);
        });

        [UnityTest]
        public IEnumerator ReleaseThrows_OtherGroupIsReleased() => UniTask.ToCoroutine(async () =>
        {
            var failing = new ProbeGroup(true);
            var other = new ProbeGroup();
            AssetGroupRequestRoots.SetFirst(failing);
            AssetGroupRequestRoots.SetSecond(other);

            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, AssetGroupRequestRoots.LoadTwo));

            await Catch(() => scope.Dispose());

            Assert.IsFalse(other.IsLoaded, "the failed release of one group stopped the release of the next one");
        });

        private static async UniTask<Exception> Catch(Func<UniTask> action)
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                return exception;
            }

            Assert.Fail("An exception was expected");
            return null;
        }

        private static async UniTask<Exception> Catch<T>(Func<UniTask<T>> action)
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                return exception;
            }

            Assert.Fail("An exception was expected");
            return null;
        }
    }
}
