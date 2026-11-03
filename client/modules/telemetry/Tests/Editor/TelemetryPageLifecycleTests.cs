using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The events of the life of the page as marks of the open session: what came before the page died tells how it died.
    public class TelemetryPageLifecycleTests
    {
        private static IEnumerable<string> MarksOf(IReadOnlyList<TelemetryBatch> batches)
        {
            return batches.SelectMany(batch => batch.Marks).Select(mark => mark.Name);
        }

        [Test]
        public void EveryEvent_IsAMark_InItsOrder()
        {
            var fixture = new TelemetryFixture(pageLifecycle: true);
            var running = fixture.Collection.Begin();

            fixture.Clock.Set(1000);
            fixture.Page.Fire(TelemetryPageEvent.Hidden);
            fixture.Page.Fire(TelemetryPageEvent.Freeze);
            fixture.Page.Fire(TelemetryPageEvent.Resume);
            fixture.Page.Fire(TelemetryPageEvent.Visible);
            fixture.Page.Fire(TelemetryPageEvent.PageHide);
            fixture.Page.Fire(TelemetryPageEvent.PageHidePersisted);
            fixture.Page.Fire(TelemetryPageEvent.PageShowPersisted);

            var batches = fixture.Collection.End(running);

            CollectionAssert.AreEqual(
                new[] { "page.hidden", "page.freeze", "page.resume", "page.visible", "page.pagehide", "page.pagehide.persisted", "page.pageshow.persisted" },
                MarksOf(batches));
            Assert.AreEqual(1000, batches.SelectMany(batch => batch.Marks).First().Time);
        }

        [Test]
        public void ManyEvents_OnlyTheFirstOnesOfASession()
        {
            var fixture = new TelemetryFixture(pageLifecycle: true);

            var running = fixture.Collection.Begin();

            for (var i = 0; i < PageLifecycleTelemetryCollector.MaxMarks + 5; i++)
                fixture.Page.Fire(TelemetryPageEvent.Hidden);

            Assert.AreEqual(PageLifecycleTelemetryCollector.MaxMarks, MarksOf(fixture.Collection.End(running)).Count());

            running = fixture.Collection.Begin();
            fixture.Page.Fire(TelemetryPageEvent.Visible);

            CollectionAssert.AreEqual(new[] { "page.visible" }, MarksOf(fixture.Collection.End(running)));
        }

        [Test]
        public void NoSession_NoMark()
        {
            var fixture = new TelemetryFixture(pageLifecycle: true);

            fixture.Page.Fire(TelemetryPageEvent.PageHide);
            var batches = fixture.Collection.End(fixture.Collection.Begin());

            CollectionAssert.IsEmpty(MarksOf(batches));
        }

        [Test]
        public void TheNames_AreValid()
        {
            var fixture = new TelemetryFixture(pageLifecycle: true);
            var running = fixture.Collection.Begin();

            foreach (TelemetryPageEvent kind in System.Enum.GetValues(typeof(TelemetryPageEvent)))
                fixture.Page.Fire(kind);

            foreach (var mark in MarksOf(fixture.Collection.End(running)))
                Assert.IsTrue(TelemetryNameGuard.IsValid(mark, TelemetryLimits.MaxNameLength), mark);
        }
    }
}
