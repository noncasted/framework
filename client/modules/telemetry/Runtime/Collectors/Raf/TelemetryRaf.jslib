var LibraryTelemetryRaf = {
    // An own chain of requestAnimationFrame: the browser runs the callbacks of a frame together, so it adds no frame, and its
    // timestamp is the start of the frame of the browser whatever Unity does in it. A frame of the browser costs it a few
    // arithmetic operations; wasm is called once per sample. A hidden page breaks the chain of the intervals.
    $telemetryRafState: {
        isRunning: false,
        isListening: false,
        handle: 0,
        last: 0,
        count: 0,
        total: 0,
        max: 0,
        tick: null
    },

    TelemetryRafStart: function () {
        const state = telemetryRafState;

        if (!state.isListening) {
            state.isListening = true;

            const reset = function () {
                state.last = 0;
            };

            document.addEventListener('visibilitychange', reset);
            window.addEventListener('pagehide', reset);

            state.tick = function (time) {
                if (!state.isRunning)
                    return;

                if (state.last > 0 && !document.hidden) {
                    const interval = time - state.last;

                    state.count++;
                    state.total += interval;

                    if (interval > state.max)
                        state.max = interval;
                }

                state.last = time;
                state.handle = requestAnimationFrame(state.tick);
            };
        }

        if (state.isRunning)
            return;

        state.isRunning = true;
        state.last = 0;
        state.count = 0;
        state.total = 0;
        state.max = 0;
        state.handle = requestAnimationFrame(state.tick);
    },

    TelemetryRafStop: function () {
        const state = telemetryRafState;

        state.isRunning = false;
        cancelAnimationFrame(state.handle);
    },

    // The average clears the counters, so the max goes first.
    TelemetryRafTakeAverage: function () {
        const state = telemetryRafState;
        const average = state.count === 0 ? -1 : state.total / state.count;

        state.count = 0;
        state.total = 0;
        state.max = 0;

        return average;
    },

    TelemetryRafTakeMax: function () {
        const state = telemetryRafState;

        return state.count === 0 ? -1 : state.max;
    }
};

autoAddDeps(LibraryTelemetryRaf, '$telemetryRafState');
mergeInto(LibraryManager.library, LibraryTelemetryRaf);
