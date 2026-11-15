using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    public class TelemetrySelfLogTests
    {
        // A nested guard gives the outer one back its state: the module's own lines after it are still the module's.
        [Test]
        public void Guard_Nested_RestoresTheOuterState()
        {
            var isInsideAfterNested = false;

            TelemetrySelfLog.Guard(() =>
            {
                TelemetrySelfLog.Guard(() => { });
                isInsideAfterNested = TelemetrySelfLog.IsInside;
            });

            Assert.IsTrue(isInsideAfterNested);
            Assert.IsFalse(TelemetrySelfLog.IsInside);
        }
    }
}
