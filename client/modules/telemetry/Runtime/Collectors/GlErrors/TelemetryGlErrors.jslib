var LibraryTelemetryGlErrors = {
    // The time of the last take: the loop is synchronous, a context behind on its work keeps the main thread here.
    $telemetryGlErrorsState: {
        lastMs: 0
    },

    // gl.getError of the context of Unity (GLctx) until it says NO_ERROR. WebGL keeps one flag per code, so a loop ends after a few; a
    // lost context gives CONTEXT_LOST_WEBGL once. A call is a round trip to the GPU process in Chromium: once a sample, never per
    // frame. Under the check of every call (TelemetryGlCallErrors.jslib) the flags are taken right after each call: the codes it
    // took since the previous take come first, each once, as the flags would have given them. The errors emscripten records
    // itself (GL.lastError) are not the context's: TelemetryGlCallErrors.jslib counts them.
    TelemetryGlErrorsTake__deps: ['$telemetryGlErrorsState', '$telemetryGlCallErrorsState'],
    TelemetryGlErrorsTake: function (into, max) {
        var count = 0;
        var start = performance.now();
        var checked = telemetryGlCallErrorsState.codes;

        try {
            while (count < max && checked.length > 0) {
                HEAP32[(into >> 2) + count] = checked.shift();
                count++;
            }

            if (typeof GLctx !== 'undefined' && GLctx) {
                while (count < max) {
                    var code = GLctx.getError();

                    if (code === 0)
                        break;

                    HEAP32[(into >> 2) + count] = code;
                    count++;
                }
            }
        } catch (error) {
        }

        telemetryGlErrorsState.lastMs = performance.now() - start;

        return count;
    },

    TelemetryGlErrorsLastMs__deps: ['$telemetryGlErrorsState'],
    TelemetryGlErrorsLastMs: function () {
        return telemetryGlErrorsState.lastMs;
    }
};

mergeInto(LibraryManager.library, LibraryTelemetryGlErrors);
