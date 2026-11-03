using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The numbers of a development build: counters a release build does not feed and the markers the game lists, per frame.
    public class DevProfilerTelemetryCollectorTests
    {
        private static TelemetryFixture FixtureOf(FakeFrameSeriesSource source, params string[] markers)
        {
            var collector = new DevProfilerTelemetryCollector(source, new ClientTelemetryOptions(devProfilerMarkers: markers));
            return new TelemetryFixture(collectors: new ITelemetryCollector[] { collector }, liveCollectors: new ITelemetryLiveCollector[] { collector });
        }

        private static Dictionary<string, double> ValuesOf(IReadOnlyList<TelemetryBatch> batches)
        {
            var values = new Dictionary<string, double>();

            foreach (var batch in batches)
            {
                foreach (var sample in batch.Samples)
                {
                    for (var slot = 0; slot < batch.Names.Count; slot++)
                    {
                        if (double.IsNaN(sample.Values[slot]) == false)
                            values[batch.Names[slot].Name] = sample.Values[slot];
                    }
                }
            }

            return values;
        }

        [Test]
        public void ACounter_IsTheAverageAndTheMaximumPerFrame()
        {
            var source = new FakeFrameSeriesSource();
            source.Counter("Memory", "GC Allocated In Frame").Frames(100, 300, 200);
            var fixture = FixtureOf(source);

            var running = fixture.Collection.Begin();
            fixture.Sampler.Sample();
            var values = ValuesOf(fixture.Collection.End(running));

            Assert.AreEqual(200d, values["dev.gc.allocated_in_frame"]);
            Assert.AreEqual(300d, values["dev.gc.allocated_in_frame.max"]);
        }

        [Test]
        public void ACounterTheBuildHasNot_HasNoMetric()
        {
            var fixture = FixtureOf(new FakeFrameSeriesSource());

            var running = fixture.Collection.Begin();
            fixture.Sampler.Sample();

            Assert.IsEmpty(ValuesOf(fixture.Collection.End(running)));
        }

        [Test]
        public void AMarkerOfTheOptions_IsInMilliseconds_ByItsName()
        {
            var source = new FakeFrameSeriesSource();
            var fixture = FixtureOf(source, "Shader.CreateGPUProgram", "Game Update/Swarm");
            source.Marker("Shader.CreateGPUProgram").Frames(2_000_000, 6_000_000);

            var running = fixture.Collection.Begin();
            fixture.Sampler.Sample();
            var values = ValuesOf(fixture.Collection.End(running));

            CollectionAssert.AreEqual(new[] { "Shader.CreateGPUProgram", "Game Update/Swarm" }, source.OpenedMarkers);
            Assert.AreEqual(4d, values["dev.marker.shader.creategpuprogram"], 1e-9);
            Assert.AreEqual(6d, values["dev.marker.shader.creategpuprogram.max"], 1e-9);
            Assert.IsFalse(values.ContainsKey("dev.marker.game_update_swarm"), "no frames, no value");
        }

        [Test]
        public void TheNameOfAMarker_IsValid_AndLeavesRoomForTheMaximum()
        {
            foreach (var marker in new[] { "Camera.Render", "Game Update/Swarm", new string('X', 100) })
            {
                var metric = DevProfilerTelemetryCollector.MarkerMetric(marker);

                Assert.IsTrue(metric.StartsWith(DevProfilerTelemetryCollector.MarkerPrefix), metric);
                Assert.IsTrue(TelemetryNameGuard.IsValid(metric + ".max", TelemetryLimits.MaxNameLength), metric);
            }
        }

        [Test]
        public void NoMarkersByDefault()
        {
            Assert.IsEmpty(new ClientTelemetryOptions().DevProfilerMarkers);
        }

        [Test]
        public void AMarkerWithoutAName_IsAMistake()
        {
            Assert.Throws<System.ArgumentException>(() => new ClientTelemetryOptions(devProfilerMarkers: new[] { " " }));
        }

        [Test]
        public void TheNextSession_OpensItsOwn()
        {
            var source = new FakeFrameSeriesSource();
            var fixture = FixtureOf(source, "Camera.Render");

            fixture.Collection.End(fixture.Collection.Begin());
            var running = fixture.Collection.Begin();
            source.Marker("Camera.Render").Frames(1_000_000);
            fixture.Sampler.Sample();

            Assert.AreEqual(2, source.OpenedMarkers.Count);
            Assert.AreEqual(1d, ValuesOf(fixture.Collection.End(running))["dev.marker.camera.render"], 1e-9);
        }
    }
}
