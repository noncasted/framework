using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The tree of a trace: a record made after an await (the stack is empty there) stays in the stage of the game it
    // belongs to instead of falling out to the root of the trace.
    [Category("Tools")]
    public class ProfilerNestingTests
    {
        [SetUp]
        public void SetUp()
        {
            ScopeLoaderProbe.Reset();
            GeneratedScopeHarness.Register(((Action<IEntityBuilder>)ScopeEntityRoots.Entity).Method);
            GeneratedScopeHarness.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.Parent).Method);
            GameProfiler.Begin(nameof(ProfilerNestingTests));
        }

        [TearDown]
        public void TearDown()
        {
            if (GameProfiler.IsRunning == true)
                Finish();
        }

        [Test]
        public void Scope_EmptyStack_GoesToTheLastStage()
        {
            GameProfiler.Stage("Meta").Dispose();
            GameProfiler.Scope("Late").Dispose();

            AssertParent(Finish(), "Late", "Meta");
        }

        [Test]
        public void Concurrent_EmptyStack_GoesToTheLastStage()
        {
            GameProfiler.Stage("Meta").Dispose();
            GameProfiler.Concurrent("Request").Dispose();

            AssertParent(Finish(), "Request", "Meta");
        }

        [Test]
        public void Detached_GoesToTheLastStageAndStaysOffTheStack()
        {
            using var stage = GameProfiler.Stage("Meta");
            using var load = GameProfiler.Scope("Load");
            var branch = GameProfiler.Detached("Connection");

            GameProfiler.Scope("Inner").Dispose();
            branch.Dispose();

            var trace = Finish();
            AssertParent(trace, "Connection", "Meta");
            AssertParent(trace, "Inner", "Load");
        }

        // A background branch is a root of its own; what it measures under its ambient nests into it, the late records keep
        // their stage.
        [Test]
        public void Background_IsARoot_OffTheStackAndNotAStage()
        {
            using var stage = GameProfiler.Stage("Meta");
            var branch = GameProfiler.Background("Music");

            using (GameProfiler.Ambient(branch))
                GameProfiler.Concurrent("Bundle").Dispose();

            GameProfiler.Scope("Inner").Dispose();
            branch.Dispose();
            GameProfiler.Scope("Late").Dispose();

            var trace = Finish();
            Assert.AreEqual(-1, Span(trace, "Music").ParentId);
            AssertParent(trace, "Bundle", "Music");
            AssertParent(trace, "Inner", "Meta");
            AssertParent(trace, "Late", "Meta");
        }

        // Every stage is a root, wherever the main chain is.
        [Test]
        public void Stage_IsARootEvenInsideAnotherStage()
        {
            using var first = GameProfiler.Stage("First");
            using var second = GameProfiler.Stage("Second");

            var trace = Finish();
            Assert.AreEqual(-1, Span(trace, "First").ParentId);
            Assert.AreEqual(-1, Span(trace, "Second").ParentId);
        }

        // A later stage takes the late records; the earlier one keeps what it had.
        [Test]
        public void Stage_NextOne_TakesTheLateRecords()
        {
            GameProfiler.Stage("Menu").Dispose();
            GameProfiler.Scope("In menu").Dispose();
            GameProfiler.Stage("Lobby").Dispose();
            GameProfiler.Scope("In lobby").Dispose();

            var trace = Finish();
            AssertParent(trace, "In menu", "Menu");
            AssertParent(trace, "In lobby", "Lobby");
        }

        [Test]
        public void Scope_NoStage_StaysAtTheRoot()
        {
            GameProfiler.Scope("Plain").Dispose();

            Assert.AreEqual(-1, Span(Finish(), "Plain").ParentId);
        }

        // The stack wins over the stage: nesting of the linear chain does not change.
        [Test]
        public void Scope_OpenScope_WinsOverTheStage()
        {
            using var stage = GameProfiler.Stage("Meta");
            using var load = GameProfiler.Scope("Load");

            GameProfiler.Scope("Inner").Dispose();

            AssertParent(Finish(), "Inner", "Load");
        }

        // A stage is as long as what it holds: its bar covers the late records.
        [Test]
        public void Finish_ParentClosedEarlier_CoversTheLateChild()
        {
            GameProfiler.Stage("Meta").Dispose();
            var late = GameProfiler.Scope("Late");
            Spin();
            late.Dispose();

            var trace = Finish();
            var parent = Span(trace, "Meta");
            var child = Span(trace, "Late");

            // The ends are equal in nanoseconds; their sums in milliseconds may differ in the last bit of a double.
            Assert.AreEqual(child.EndNs, parent.EndNs);
            Assert.AreEqual(child.StartMs + child.DurationMs, parent.StartMs + parent.DurationMs, 1e-6);
            Assert.AreEqual(child.EndFrame, parent.EndFrame);
            Assert.GreaterOrEqual(parent.AllocBytes, child.AllocBytes);
        }

        // The cover goes up the whole chain.
        [Test]
        public void Finish_LateGrandchild_CoversEveryParent()
        {
            GameProfiler.Stage("Meta").Dispose();
            var connection = GameProfiler.Detached("Connection");
            connection.Dispose();
            var late = connection.Child("Late");
            late.Start();
            Spin();
            late.Dispose();

            var trace = Finish();
            Assert.AreEqual(Span(trace, "Late").EndNs, Span(trace, "Connection").EndNs);
            Assert.AreEqual(Span(trace, "Late").EndNs, Span(trace, "Meta").EndNs);
        }

        // A child that never started says nothing about the end of its parent.
        [Test]
        public void Finish_ChildNeverStarted_ParentKeepsItsEnd()
        {
            var parent = GameProfiler.Stage("Meta");
            parent.Child("Never");
            Spin();
            parent.Dispose();
            var expected = parent.DurationMs;

            Assert.AreEqual(expected, Span(Finish(), "Meta").DurationMs, 0.0001d);
        }

        [Test]
        public void Stage_NoTrace_IsTheNullScope()
        {
            Finish();

            Assert.AreSame(NullProfilerScope.Instance, GameProfiler.Stage("Meta"));
        }

        // The phases of an entity come long after the load of its scope: they are kept together under the entity.
        [UnityTest]
        public IEnumerator EntityLoad_PhasesGoUnderTheEntity() => UniTask.ToCoroutine(async () =>
        {
            GameProfiler.Stage("Lobby").Dispose();

            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);

            await new EntityScopeLoader().Load(parentLifetime, parent, new ScopeTestEntityView(), ScopeEntityRoots.Entity);
            parentLifetime.Terminate();
            parent.Dispose();

            var trace = Finish();
            var entity = $"Entity: {nameof(ScopeTestEntityView)}";
            AssertParent(trace, entity, "Lobby");
            AssertParent(trace, "Resolve: IScopeBaseSetup", entity);
            AssertParent(trace, "Resolve: IScopeLoaded", entity);
        });

        // The root of the chain is a stage, so what follows the load of the root scope stays in it.
        [UnityTest]
        public IEnumerator LoadRoot_OpensTheStartupStage() => UniTask.ToCoroutine(async () =>
        {
            var scope = await new ServiceScopeLoader().LoadRoot(ScopeLoaderRoots.Parent);

            GameProfiler.Scope("Late").Dispose();
            var trace = Finish();
            await scope.Dispose();

            AssertParent(trace, "Late", "Startup");
        });

        // The next stage holds what the previous one leaves behind: a scope disposed before the next one loads.
        [UnityTest]
        public IEnumerator ScopeDispose_AfterItsStage_GoesToThatStage() => UniTask.ToCoroutine(async () =>
        {
            ILoadedScope scope;

            using (GameProfiler.Stage("Menu"))
                scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ScopeLoaderRoots.Parent));

            await scope.Dispose();

            AssertParent(Finish(), "Resolve: IScopeDispose", "Menu");
        });

        private static ProfilerTraceData Finish()
        {
            GameProfiler.Finish();

            var trace = ProfilerTraceStorage.Last;

            // The trace is a real file next to the project.
            var directory = Path.GetDirectoryName(ProfilerTraceStorage.LastPath);
            File.Delete(ProfilerTraceStorage.LastPath);

            if (Directory.GetFileSystemEntries(directory).Length == 0)
                Directory.Delete(directory);

            return trace;
        }

        private static void AssertParent(ProfilerTraceData trace, string name, string parent)
        {
            var span = Span(trace, name);

            Assert.GreaterOrEqual(span.ParentId, 0, $"{name} fell out to the root of the trace");
            Assert.AreEqual(parent, trace.Spans.First(item => item.Id == span.ParentId).Name);
        }

        private static ProfilerSpanData Span(ProfilerTraceData trace, string name)
        {
            return trace.Spans.First(item => item.Name == name);
        }

        private static void Spin()
        {
            var watch = Stopwatch.StartNew();

            while (watch.Elapsed.TotalMilliseconds < 2d)
            {
            }
        }
    }
}
