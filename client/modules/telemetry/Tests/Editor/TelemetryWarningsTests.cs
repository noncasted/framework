using System.Linq;
using NUnit.Framework;
using Shared;
using UnityEngine;

namespace Global.Telemetry.Tests
{
    // The warnings of the log of Unity (TextMeshPro, a texture that failed) go with the errors under their own hard limit, so a game
    // that warns every frame neither costs nor pushes the errors out.
    public class TelemetryWarningsTests
    {
        private static TelemetryBatchError[] ErrorsOf(System.Collections.Generic.IReadOnlyList<TelemetryBatch> batches)
        {
            return batches.SelectMany(batch => batch.Errors).ToArray();
        }

        [Test]
        public void AWarning_GoesAsItsLevel()
        {
            var fixture = new TelemetryFixture(catchErrors: true);
            var running = fixture.Collection.Begin();

            Debug.LogWarning("The character was not found in the font");

            var error = ErrorsOf(fixture.Collection.End(running)).Single();
            Assert.AreEqual(TelemetryErrorLevel.Warning, error.Level);
            StringAssert.StartsWith("The character was not found", error.Message);
        }

        [Test]
        public void ManyKindsOfWarnings_OnlyTheirLimit_AndTheErrorsStillGo()
        {
            var fixture = new TelemetryErrorFixture();

            for (var i = 0; i < TelemetryErrorLimits.MaxWarningGroups + 10; i++)
                fixture.Groups.Count(TelemetryErrorLevel.Warning, null, $"w{i}", string.Empty, 1);

            for (var i = 0; i < TelemetryErrorLimits.MaxGroups; i++)
                fixture.Groups.Count(TelemetryErrorLevel.Error, null, $"e{i}", string.Empty, 1);

            fixture.Groups.FlushAll();

            var entries = fixture.Records.Entries;
            Assert.AreEqual(TelemetryErrorLimits.MaxWarningGroups, entries.Count(entry => entry.Level == TelemetryErrorLevel.Warning));
            Assert.AreEqual(TelemetryErrorLimits.MaxGroups, entries.Count(entry => entry.Level == TelemetryErrorLevel.Error));
        }

        // A warning every frame for an hour: its records stop at the limit of the warnings, the errors keep all of theirs.
        [Test]
        public void AWarningForEver_StopsAtItsRecords_TheErrorsDoNot()
        {
            var fixture = new TelemetryErrorFixture();

            for (var second = 1; second < 4000; second++)
            {
                fixture.Clock.Set(second * TelemetryErrorLimits.MaxWindowMs);
                fixture.Groups.Count(TelemetryErrorLevel.Warning, null, "w", string.Empty, 1);
                fixture.Groups.FlushDue();
            }

            Assert.AreEqual(TelemetryErrorLimits.MaxWarningRecordsPerSession, fixture.Records.Entries.Count);
            Assert.IsFalse(fixture.Groups.IsSpent);

            fixture.Groups.Count(TelemetryErrorLevel.Error, null, "e", string.Empty, 1);
            fixture.Groups.FlushAll();

            Assert.AreEqual(TelemetryErrorLevel.Error, fixture.Records.Entries.Last().Level);
        }

        // In a window the errors go first.
        [Test]
        public void AWindow_TakesTheErrorsFirst()
        {
            var fixture = new TelemetryErrorFixture();

            for (var i = 0; i < TelemetryErrorLimits.MaxRecordsPerWindow; i++)
                fixture.Groups.Count(TelemetryErrorLevel.Warning, null, $"w{i}", string.Empty, 1);

            fixture.Groups.Count(TelemetryErrorLevel.Error, null, "e", string.Empty, 1);
            fixture.Clock.Set(TelemetryErrorLimits.FirstWindowMs);
            fixture.Groups.FlushDue();

            Assert.AreEqual(TelemetryErrorLevel.Error, fixture.Records.Entries[0].Level);
            Assert.AreEqual(TelemetryErrorLimits.MaxRecordsPerWindow, fixture.Records.Entries.Count);
        }

        [Test]
        public void TheQueue_HoldsFewWarnings_SoTheErrorsHaveRoom()
        {
            var queue = new TelemetryErrorQueue();
            queue.Open();

            for (var i = 0; i < TelemetryErrorLimits.MaxQueued; i++)
                queue.Add(TelemetryErrorLevel.Warning, $"w{i}", string.Empty);

            queue.Add(TelemetryErrorLevel.Error, "e", string.Empty);

            Assert.AreEqual(TelemetryErrorLimits.MaxQueuedWarnings + 1, queue.Count);
        }

        [Test]
        public void AWarning_OnTheWire_IsTheWarningOfTheContract()
        {
            Assert.AreEqual(SharedClientTelemetry.ErrorLevel.Warning, TelemetryMessages.ToLevel(TelemetryErrorLevel.Warning));
        }
    }
}
