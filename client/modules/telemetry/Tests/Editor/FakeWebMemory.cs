namespace Global.Telemetry.Tests
{
    public class FakeWebMemory : ITelemetryWebMemory
    {
        public FakeWebMemory(double heap, double jsUsed, double jsTotal, double jsLimit)
        {
            Heap = heap;
            JsUsed = jsUsed;
            JsTotal = jsTotal;
            JsLimit = jsLimit;
        }

        public double Heap { get; }
        public double JsUsed { get; }
        public double JsTotal { get; }
        public double JsLimit { get; }
    }
}
