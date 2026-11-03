using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    // A snapshot of an open trace: it shows what is there and leaves no mark on what the finish saves.
    [Category("Tools")]
    public class ProfilerSnapshotTests
    {
        private static readonly PropertyInfo _startup = typeof(GameProfiler).GetProperty(nameof(GameProfiler.Startup));

        // What the finishes of a test wrote: the folder of the traces stays as it was.
        private string[] _traces;

        [SetUp]
        public void RememberTraces()
        {
            _traces = ProfilerTraceStorage.List().ToArray();
        }

        [TearDown]
        public void DeleteNewTraces()
        {
            foreach (var path in ProfilerTraceStorage.List().Except(_traces))
                File.Delete(path);

            if (Directory.Exists(ProfilerTraceStorage.Directory) && Directory.GetFileSystemEntries(ProfilerTraceStorage.Directory).Length == 0)
                Directory.Delete(ProfilerTraceStorage.Directory);
        }

        // The tree where the finish does the most: a parent that closed before its child, siblings started out of order.
        private static IProfilerScope[] Stages(Profiler profiler)
        {
            var late = profiler.CreateScope("Late");
            var early = profiler.CreateScope("Early");
            var child = early.Child("Child");
            var open = early.Child("Open");

            early.Start();
            child.Start();
            System.Threading.Thread.Sleep(2);
            late.Start();
            open.Start();
            early.Stop();
            System.Threading.Thread.Sleep(2);
            child.Stop();

            return new[] { early, late, child, open };
        }

        private static object[] Shape(ProfilerTraceData trace)
        {
            return trace.Spans
                        .Select(span => (object)(span.Id, span.ParentId, span.Depth, span.Name, span.Unfinished))
                        .ToArray();
        }

        [Test]
        public void Snapshot_OpenSpans_Unfinished_ClosedNot()
        {
            var profiler = new Profiler("Trace");

            Stages(profiler);

            var snapshot = profiler.Snapshot();

            Assert.IsTrue(snapshot.Spans.Single(span => span.Name == "Open").Unfinished);
            Assert.IsTrue(snapshot.Spans.Single(span => span.Name == "Late").Unfinished);
            Assert.IsFalse(snapshot.Spans.Single(span => span.Name == "Child").Unfinished);
            Assert.IsFalse(snapshot.Spans.Single(span => span.Name == "Early").Unfinished);
        }

        [Test]
        public void Snapshot_Hierarchy_ParentBelowChild_IdIsTheIndex()
        {
            var profiler = new Profiler("Trace");

            Stages(profiler);

            var spans = profiler.Snapshot().Spans;

            for (var index = 0; index < spans.Count; index++)
            {
                Assert.AreEqual(index, spans[index].Id);
                Assert.Less(spans[index].ParentId, index);
            }

            Assert.AreEqual("Early", spans[0].Name);
            Assert.AreEqual(-1, spans[0].ParentId);
            Assert.AreEqual(0, spans[1].ParentId);
        }

        // The end of a closed parent stays its own: covering the children is the business of the finish.
        [Test]
        public void Snapshot_DoesNotMoveTheEndOfAClosedParent()
        {
            var profiler = new Profiler("Trace");
            var scopes = Stages(profiler);
            var duration = scopes[0].DurationMs;

            var snapshot = profiler.Snapshot();

            Assert.AreEqual(duration, scopes[0].DurationMs);
            Assert.AreEqual(duration, snapshot.Spans.Single(span => span.Name == "Early").DurationMs);
            Assert.Less(duration, snapshot.Spans.Single(span => span.Name == "Child").DurationMs + 2d);
        }

        [Test]
        public void Snapshot_DoesNotChangeTheFinalTrace()
        {
            var with = new Profiler("Trace");
            var without = new Profiler("Trace");

            var scopes = Stages(with);
            Stages(without);

            var childrenBefore = scopes[0].Children.Select(child => child.Name).ToArray();

            with.Snapshot();
            with.Snapshot();

            CollectionAssert.AreEqual(childrenBefore, scopes[0].Children.Select(child => child.Name).ToArray());
            Assert.IsTrue(scopes[3].IsRunning);
            Assert.IsNull(with.Final);

            var durationBefore = scopes[2].DurationMs;

            with.Finish();
            without.Finish();

            Assert.AreEqual(durationBefore, scopes[2].DurationMs);
            CollectionAssert.AreEqual(Shape(without.Final), Shape(with.Final));
        }

        [Test]
        public void Finish_KeepsTheFinal_TheOneOfTheEvent()
        {
            var profiler = new Profiler("Trace");
            ProfilerTraceData saved = null;

            void OnSaved(ProfilerTraceData trace) => saved = trace;

            ProfilerTraceStorage.Saved += OnSaved;
            profiler.Finish();
            ProfilerTraceStorage.Saved -= OnSaved;

            Assert.AreSame(saved, profiler.Final);
        }

        [Test]
        public void Startup_IsTheFirstTraceOfTheRun()
        {
            _startup.SetValue(null, null);

            var first = GameProfiler.Begin("First");
            GameProfiler.Finish();
            GameProfiler.Begin("Second");

            Assert.AreSame(first, GameProfiler.Startup);
            Assert.AreEqual("First", GameProfiler.Startup.Final.Name);

            GameProfiler.Finish();

            Assert.AreEqual("First", GameProfiler.Startup.Final.Name);
        }
    }
}
