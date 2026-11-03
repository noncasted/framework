using System.Collections.Generic;

namespace Global.Telemetry
{
    // The errors of the calls of WebGL with the name of the call: the ones emscripten records itself, and under the check of every call
    // (a page opened with ?glcheck) the ones of the context. TelemetryGlCallErrors.jslib.
    public interface ITelemetryGlCallErrors
    {
        // The counters since the previous take; the first error of each source, code and call of the page is added to the list once.
        TelemetryGlCallErrorCounts Take(List<TelemetryGlCallError> details);
    }
}
