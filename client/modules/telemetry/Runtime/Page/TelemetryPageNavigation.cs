using System;

namespace Global.Telemetry
{
    // PerformanceNavigationTiming of the document. 0: the browser did not give it (no redirect, a connection reused, no entry).
    [Serializable]
    public class TelemetryPageNavigation
    {
        public double redirectStart;
        public double redirectEnd;
        public double domainLookupStart;
        public double domainLookupEnd;
        public double connectStart;
        public double connectEnd;
        public double requestStart;
        public double responseStart;
        public double responseEnd;
        public double domContentLoadedEventEnd;
        public double loadEventEnd;
    }
}
