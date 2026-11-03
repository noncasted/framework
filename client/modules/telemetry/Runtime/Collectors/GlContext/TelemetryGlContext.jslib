var LibraryTelemetryGlContext = {
    // Kept here, not on the managed side: a loss without a session waits for the next one, and a scope loaded again does not count
    // it twice. The default action of webglcontextlost is left alone: the behaviour of the game does not change for the telemetry.
    $telemetryGlContextState: {
        isListening: false,
        isLost: false,
        lost: 0,
        restored: 0,
        onChanged: null
    },

    // Between the frames, as any event. A wasm that is dead at this point has nothing to say to: the counters go on, the page hears
    // nothing about it.
    $telemetryGlContextNotify: function () {
        var state = telemetryGlContextState;

        if (state.onChanged === null)
            return;

        try {
            {{{ makeDynCall('v', 'state.onChanged') }}}();
        } catch (error) {
            state.onChanged = null;
        }
    },

    // A scope loaded again only takes the callback: the listeners live as long as the canvas, the page. Module.canvas is what the
    // loader of Unity got from the page.
    TelemetryGlContextListen__deps: ['$telemetryGlContextNotify'],
    TelemetryGlContextListen: function (onChanged) {
        var state = telemetryGlContextState;

        state.onChanged = onChanged;

        if (state.isListening)
            return;

        state.isListening = true;

        Module.canvas.addEventListener('webglcontextlost', function () {
            state.isLost = true;
            state.lost++;
            telemetryGlContextNotify();
        });

        Module.canvas.addEventListener('webglcontextrestored', function () {
            state.isLost = false;
            state.restored++;
            telemetryGlContextNotify();
        });
    },

    TelemetryGlContextIsLost: function () {
        return telemetryGlContextState.isLost;
    },

    TelemetryGlContextTakeLost: function () {
        var lost = telemetryGlContextState.lost;

        telemetryGlContextState.lost = 0;

        return lost;
    },

    TelemetryGlContextTakeRestored: function () {
        var restored = telemetryGlContextState.restored;

        telemetryGlContextState.restored = 0;

        return restored;
    }
};

autoAddDeps(LibraryTelemetryGlContext, '$telemetryGlContextState');
mergeInto(LibraryManager.library, LibraryTelemetryGlContext);
