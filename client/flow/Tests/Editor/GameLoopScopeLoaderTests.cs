using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    public class GameLoopScopeLoaderTests
    {
        // After a failed load there is no current scope: the next load must not await the old unloading again.
        [UnityTest]
        public IEnumerator Load_AfterFailedLoad_Succeeds() => UniTask.ToCoroutine(async () =>
        {
            var loader = new GameLoopScopeLoader(null, null);
            var first = new FakeScope();
            var third = new FakeScope();

            await loader.Load((_, _) => UniTask.FromResult<ILoadedScope>(first));
            await Catch(loader.Load((_, _) => UniTask.FromException<ILoadedScope>(new InvalidOperationException("load failed"))));
            var loaded = await loader.Load((_, _) => UniTask.FromResult<ILoadedScope>(third));

            Assert.AreSame(third, loaded);
            Assert.AreEqual(1, first.DisposeCount);
        });

        // A failed dispose is reported to the load that ran it, it does not fail every load after it.
        [UnityTest]
        public IEnumerator Load_AfterFailedDispose_Succeeds() => UniTask.ToCoroutine(async () =>
        {
            var loader = new GameLoopScopeLoader(null, null);
            var failing = new FakeScope { DisposeError = new InvalidOperationException("dispose failed") };
            var next = new FakeScope();

            await loader.Load((_, _) => UniTask.FromResult<ILoadedScope>(failing));
            var caught = await Catch(loader.Load((_, _) => UniTask.FromResult<ILoadedScope>(new FakeScope())));
            var loaded = await loader.Load((_, _) => UniTask.FromResult<ILoadedScope>(next));

            Assert.AreEqual("dispose failed", caught.Message);
            Assert.AreSame(next, loaded);
        });

        // Loads run one after another: the second one disposes the scope of the first.
        [UnityTest]
        public IEnumerator Load_Overlapping_DisposesTheFirstScope() => UniTask.ToCoroutine(async () =>
        {
            var loader = new GameLoopScopeLoader(null, null);
            var gate = new UniTaskCompletionSource();
            var first = new FakeScope();
            var second = new FakeScope();

            var firstLoad = loader.Load(async (_, _) =>
            {
                await gate.Task;
                return first;
            });
            var secondLoad = loader.Load((_, _) => UniTask.FromResult<ILoadedScope>(second));

            gate.TrySetResult();
            await firstLoad;
            var loaded = await secondLoad;

            Assert.AreSame(second, loaded);
            Assert.AreEqual(1, first.DisposeCount, "the scope of the first load was never disposed");
            Assert.AreEqual(0, second.DisposeCount);
        });

        private static async UniTask<Exception> Catch(UniTask<ILoadedScope> task)
        {
            try
            {
                await task;
            }
            catch (Exception exception)
            {
                return exception;
            }

            Assert.Fail("expected an exception");
            return null;
        }

        private sealed class FakeScope : ILoadedScope
        {
            public int DisposeCount;
            public Exception DisposeError;

            public IContainer Container => throw new NotSupportedException();
            public IReadOnlyLifetime Lifetime => throw new NotSupportedException();

            // A real asynchronous dispose: its UniTask may be awaited only once.
            public async UniTask Dispose()
            {
                DisposeCount++;
                await UniTask.Yield();

                if (DisposeError != null)
                    throw DisposeError;
            }
        }
    }
}
