var LibraryTelemetryGlErrors = {
    // The time of the last take: the loop is synchronous, a context behind on its work keeps the main thread here.
    $telemetryGlErrorsState: {
        lastMs: 0
    },

    // gl.getError of the context of Unity (GLctx) until it says NO_ERROR, then the error emscripten recorded itself (GL.lastError,
    // what glGetError of the build would return next). WebGL keeps one flag per code, so a loop ends after a few; a lost context
    // gives CONTEXT_LOST_WEBGL once. A call is a round trip to the GPU process in Chromium: once a sample, never per frame.
    TelemetryGlErrorsTake__deps: ['$GL', '$telemetryGlErrorsState'],
    TelemetryGlErrorsTake: function (into, max) {
        var count = 0;
        var start = performance.now();

        try {
            if (typeof GLctx !== 'undefined' && GLctx) {
                while (count < max) {
                    var code = GLctx.getError();

                    if (code === 0)
                        break;

                    HEAP32[(into >> 2) + count] = code;
                    count++;
                }
            }

            if (count < max && GL.lastError) {
                HEAP32[(into >> 2) + count] = GL.lastError;
                GL.lastError = 0;
                count++;
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
