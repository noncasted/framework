using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
            ContainerTestRoots.Register(root);
        }

        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        [UnityTest]
        public IEnumerator Scope_TwoCollectors_BothInTheBatches() => UniTask.ToCoroutine(async () =>
        {
            ContainerProbe.Reset();
            RegisterGenerated(ContainerRoots.WithCollectors);
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ContainerRoots.WithCollectors));

            await scope.Initialize();
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

            await scope.Initialize();
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

            await scope.Initialize();
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

            await scope.Initialize();
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

            await scope.Initialize();
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

            await scope.Initialize();
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

            await scope.Initialize();
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
            await meta.Initialize();
            await UniTask.Delay(50, DelayType.Realtime);
            Assert.IsEmpty(ContainerProbe.Writer.Requests);

            var below = await new ServiceScopeLoader().Load(new ScopeLoadOptions(meta, ContainerRoots.BelowTheMeta));
            await below.Initialize();
            await UniTask.WaitUntil(() => ContainerProbe.Writer.Requests.Count > 0).Timeout(_timeout);

            await below.Dispose();
            await meta.Dispose();

            Assert.IsInstanceOf<SharedClientTelemetry.SessionStart>(ContainerProbe.Writer.Requests[0]);
        });

        private static string[] NamesSent()
        {
            return ContainerProbe.Transport.Batches.SelectMany(call => TelemetryFixture.NamesOf(call.Batch)).ToArray();
        }

        // Rule 2 of the framework: the core and the other modules know nothing about this one.
        [Test]
        public void Module_NothingElseReferencesIt()
        {
            var name = typeof(ClientTelemetry).Assembly.GetName().Name;

            var referencing = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name.StartsWith("Internal.Telemetry") == false)
                .Where(assembly => assembly.GetReferencedAssemblies().Any(reference => reference.Name == name))
                .Select(assembly => assembly.GetName().Name)
                .ToArray();

            CollectionAssert.IsEmpty(referencing);
        }
    }

    public static class ContainerTestRoots
    {
        // RuntimeInitializeOnLoadMethod is not called in edit mode, so the generated classes are registered here.
        public static void Register(Func<IScopeBuilder, UniTask> root)
        {
            var name = root.Method.DeclaringType.Name + root.Method.Name + "Container";
            var type = root.Method.DeclaringType.Assembly.GetType(root.Method.DeclaringType.Namespace + "." + name, true);

            if (GeneratedScopes.IsRegistered(GeneratedScopes.RootId(root.Method)) == false)
                type.GetMethod("RegisterGenerated", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
    }

    public static class ContainerProbe
    {
        public static RecordingTransport Transport { get; private set; }
        public static IUpdater Updater { get; private set; }
        public static ScriptedTelemetryWriter Writer { get; private set; }
        public static ITelemetrySocket Socket { get; private set; }

        public static void SetUpdater(IUpdater updater)
        {
            Updater = updater;
        }

        public static void Reset()
        {
            Transport = new RecordingTransport();
            Writer = new ScriptedTelemetryWriter();
            Socket = new FakeTelemetrySocket(Writer);
        }
    }

    // What a game does: opens a session when it gets to a part of the game, here on the lifetime of the scope.
    public class SessionOnLoad : IScopeLoaded
    {
        public SessionOnLoad(IClientTelemetry telemetry)
        {
            _telemetry = telemetry;
        }

        private readonly IClientTelemetry _telemetry;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _telemetry.StartSession(lifetime, "probe");
        }
    }

    public static class ContainerRoots
    {
        private static ClientTelemetryOptions Options(bool enabledInEditor)
        {
            return new ClientTelemetryOptions(
                sendIntervalSeconds: 0.01f,
                sampleIntervalSeconds: 0.01f,
                enabledInEditor: enabledInEditor);
        }

        private static void AddTelemetry(IScopeBuilder builder, bool enabledInEditor)
        {
            builder.RegisterInstance<ITelemetryTransport>(ContainerProbe.Transport);
            builder.AddClientTelemetry(Options(enabledInEditor));
        }

        [ContainerGraphRoot]
        public static UniTask WithCollectors(IScopeBuilder builder)
        {
            AddTelemetry(builder, true);
            builder.Register<SessionOnLoad>().As<IScopeLoaded>();
            builder.Register<FirstCollector>().As<ITelemetryCollector>();
            builder.Register<SecondCollector>().As<ITelemetryCollector>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask WithoutCollectors(IScopeBuilder builder)
        {
            AddTelemetry(builder, true);
            builder.Register<SessionOnLoad>().As<IScopeLoaded>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask WithoutSession(IScopeBuilder builder)
        {
            AddTelemetry(builder, true);
            builder.Register<FirstCollector>().As<ITelemetryCollector>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask WithStandardCollectors(IScopeBuilder builder)
        {
            builder.RegisterInstance<IUpdater>(ContainerProbe.Updater);
            AddTelemetry(builder, true);
            builder.Register<SessionOnLoad>().As<IScopeLoaded>();
            builder.AddStandardTelemetryCollectors();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask OverTheSocket(IScopeBuilder builder)
        {
            builder.RegisterInstance<ITelemetrySocket>(ContainerProbe.Socket);
            builder.AddSocketClientTelemetry(Options(true));
            builder.Register<SessionOnLoad>().As<IScopeLoaded>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask MetaOfAGame(IScopeBuilder builder)
        {
            builder.RegisterInstance<ITelemetrySocket>(ContainerProbe.Socket);
            builder.AddSocketClientTelemetry(Options(true));
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(ContainerRoots), nameof(MetaOfAGame))]
        public static UniTask BelowTheMeta(IScopeBuilder builder)
        {
            builder.Register<SessionOnLoad>().As<IScopeLoaded>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask OffInEditor(IScopeBuilder builder)
        {
            AddTelemetry(builder, false);
            builder.Register<SessionOnLoad>().As<IScopeLoaded>();
            return UniTask.CompletedTask;
        }
    }

    public class FirstCollector : ITelemetryCollector
    {
        public void Collect(ITelemetrySample sample)
        {
            sample.Gauge("first", 1);
        }
    }

    public class SecondCollector : ITelemetryCollector
    {
        public void Collect(ITelemetrySample sample)
        {
            sample.Gauge("second", 2);
        }
    }
}
