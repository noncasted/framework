using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // AddClientTelemetry through the generated container: collectors of the game come as a list, none is fine, the
    // transport comes from the scope of the game; the module stays optional and opens no session by itself.
    public class ClientTelemetryContainerTests
    {
        private static void RegisterGenerated(Func<IScopeBuilder, UniTask> root)
        {
            GeneratedRoots.Register(root.Method);
        }

        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        // The game enters the part of the scope once it is loaded: the call its loader makes.
        private static void Enter(ILoadedScope scope)
        {
            scope.Resolve<SessionOnLoad>().Enter(scope.Lifetime);
        }

        [UnityTest]
        public IEnumerator Scope_TwoCollectors_BothInTheBatches() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.WithCollectors);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.WithCollectors));

            Enter(scope);
            await UniTask.WaitUntil(() => NamesSent().Contains("second")).Timeout(_timeout);
            await scope.Dispose();

            CollectionAssert.AreEquivalent(new[] { "first", "second" }, NamesSent());
            var start = ContainerProbe.Transport.Starts.Single();
            Assert.IsNotNull(start.Start.Device.Platform);
            CollectionAssert.AreEqual(new[] { "probe" }, start.Start.Tags);
        });

        [UnityTest]
        public IEnumerator Scope_NoCollectors_StartsTheSession() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.WithoutCollectors);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.WithoutCollectors));

            Enter(scope);
            await UniTask.WaitUntil(() => ContainerProbe.Transport.Starts.Count > 0).Timeout(_timeout);
            await scope.Dispose();

            Assert.AreEqual(1, ContainerProbe.Transport.Starts.Count);
        });

        // Nothing in the scope opens a session but the game.
        [UnityTest]
        public IEnumerator Scope_NobodyStartsASession_NoCall() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.WithoutSession);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.WithoutSession));

            await UniTask.Delay(100, DelayType.Realtime);
            await scope.Dispose();

            Assert.IsEmpty(ContainerProbe.Transport.Calls);
        });

        [UnityTest]
        public IEnumerator Scope_OffInEditor_NoCall() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.OffInEditor);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.OffInEditor));

            Enter(scope);
            await UniTask.Delay(100, DelayType.Realtime);
            await scope.Dispose();

            Assert.IsEmpty(ContainerProbe.Transport.Calls);
        });

        // One line in the scope of a game: the collectors find the updater of the scope, the metrics reach the batches.
        // The batchmode editor gives no frames here (PlayMode tests have them), so the memory stands for the rest.
        [UnityTest]
        public IEnumerator Scope_StandardCollectors_BuiltAndCollecting() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            var updater = new GameObject("updater").AddComponent<Updater>();
            ContainerProbe.SetUpdater(updater);
            RegisterGenerated(ContainerRoots.WithStandardCollectors);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.WithStandardCollectors));

            Enter(scope);
            await UniTask.WaitUntil(() => NamesSent().Contains("gc.collections")).Timeout(_timeout);
            await scope.Dispose();
            UnityEngine.Object.DestroyImmediate(updater.gameObject);

            var names = NamesSent();
            CollectionAssert.Contains(names, "gc.collections");
            CollectionAssert.Contains(names, "memory.mono.used");
            CollectionAssert.DoesNotContain(names, "web.wasm.heap");
        });

        [UnityTest]
        public IEnumerator Scope_ErrorOfTheLog_InTheBatches() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.WithoutCollectors);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.WithoutCollectors));

            Enter(scope);
            Debug.LogError("Broken in the scope");
            await UniTask.WaitUntil(() => ContainerProbe.Transport.Batches
                         .SelectMany(call => call.Batch.Errors)
                         .Any(error => error.Message.Contains("Broken in the scope")))
                         .Timeout(_timeout);
            await scope.Dispose();

            await UniTask.Delay(100, DelayType.Realtime);
            var calls = ContainerProbe.Transport.Calls.Count;
            Debug.LogError("After the scope");
            await UniTask.Delay(100, DelayType.Realtime);

            Assert.AreEqual(calls, ContainerProbe.Transport.Calls.Count);
            Assert.AreEqual(1, ContainerProbe.Transport.Ends.Count);
        });

        // One call over the socket of the scope: the session reaches the connection.
        [UnityTest]
        public IEnumerator Scope_OverTheSocket_ReachesTheConnection() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.OverTheSocket);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.OverTheSocket));

            Enter(scope);
            await UniTask.WaitUntil(() => ContainerProbe.Writer.Requests.Count > 0).Timeout(_timeout);
            await scope.Dispose();

            var start = (SharedClientTelemetry.SessionStart)ContainerProbe.Writer.Requests[0];
            CollectionAssert.AreEqual(new[] { "probe" }, start.Tags);
        });

        // The layout of a game: the socket and the telemetry in the scope of the meta, the session opened in a scope below.
        [UnityTest]
        public IEnumerator Scope_SessionInAScopeBelowTheMeta() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.MetaOfAGame);
            RegisterGenerated(ContainerRoots.BelowTheMeta);
            var meta = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.MetaOfAGame));
            await UniTask.Delay(50, DelayType.Realtime);
            Assert.IsEmpty(ContainerProbe.Writer.Requests);

            var below = await new ServiceScopeLoader().Load(new ScopeLoadOptions(meta, ContainerRoots.BelowTheMeta));
            Enter(below);
            await UniTask.WaitUntil(() => ContainerProbe.Writer.Requests.Count > 0).Timeout(_timeout);

            await below.Dispose();
            await meta.Dispose();

            Assert.IsInstanceOf<SharedClientTelemetry.SessionStart>(ContainerProbe.Writer.Requests[0]);
        });

        // The transport over HTTP needs nothing from the scope of the game: no socket and no network module.
        [UnityTest]
        public IEnumerator Scope_OverHttp_Builds() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.OverHttp);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.OverHttp));

            await scope.Dispose();
        });

        private static string[] NamesSent()
        {
            return ContainerProbe.Transport.Batches.SelectMany(call => TelemetryFixture.NamesOf(call.Batch)).ToArray();
        }

        // Rule 2 of the framework: the core and the other modules know nothing about this one. A module plugs in only
        // through its bridge assembly <module>.Telemetry, compiled with FRAMEWORK_TELEMETRY (framerate, resolution).
        [Test]
        public void Module_NothingElseReferencesIt()
        {
            var name = typeof(ClientTelemetry).Assembly.GetName().Name;

            var referencing = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name.StartsWith("Internal.Telemetry") == false)
                .Where(assembly => assembly.GetName().Name.EndsWith(".Telemetry") == false)
                .Where(assembly => assembly.GetReferencedAssemblies().Any(reference => reference.Name == name))
                .Select(assembly => assembly.GetName().Name)
                .ToArray();

            CollectionAssert.IsEmpty(referencing);
        }
    }
}
