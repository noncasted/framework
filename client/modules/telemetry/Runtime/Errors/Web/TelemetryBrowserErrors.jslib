var LibraryTelemetryBrowserErrors = {
    // Only the text and the stack of an error go over: the file name and the address of the page are not even read. Both
    // texts are scrubbed on the managed side, where a stack also loses its urls.
    // A loop of errors is bounded here too: every call makes managed strings.
    $telemetryBrowserErrorsState: {
        maxMessage: 2048,
        maxStack: 8192,
        maxPerSecond: 16,
        windowStart: 0,
        inWindow: 0,
        isInside: false,
        callback: null,
        onError: null,
        onRejection: null
    },

    $telemetryBrowserErrorsReport__deps: ['TelemetryBrowserErrorsStop'],
    $telemetryBrowserErrorsReport: function (message, stack) {
        var state = telemetryBrowserErrorsState;

        // An error inside the call below: a dead wasm throws on any entry.
        if (state.isInside)
            return;

        var now = Date.now();

        if (now - state.windowStart >= 1000) {
            state.windowStart = now;
            state.inWindow = 0;
        }

        if (state.inWindow >= state.maxPerSecond)
            return;

        state.inWindow++;
        state.isInside = true;

        var messagePointer = 0;
        var stackPointer = 0;

        try {
            var messageText = String(message).slice(0, state.maxMessage);
            var stackText = String(stack).slice(0, state.maxStack);
            var messageSize = lengthBytesUTF8(messageText) + 1;
            var stackSize = lengthBytesUTF8(stackText) + 1;

            messagePointer = _malloc(messageSize);
            stackPointer = _malloc(stackSize);
            stringToUTF8(messageText, messagePointer, messageSize);
            stringToUTF8(stackText, stackPointer, stackSize);

            {{{ makeDynCall('vii', 'state.callback') }}}(messagePointer, stackPointer);
        } catch (error) {
            // The boundary of the page: the module has no way to the managed side any more, the page must not hear about
            // it. Listening further would only repeat this.
            _TelemetryBrowserErrorsStop();
        } finally {
            if (messagePointer)
                _free(messagePointer);

            if (stackPointer)
                _free(stackPointer);

            state.isInside = false;
        }
    },

    TelemetryBrowserErrorsListen__deps: ['$telemetryBrowserErrorsReport', 'TelemetryBrowserErrorsStop'],
    TelemetryBrowserErrorsListen: function (callback) {
        var state = telemetryBrowserErrorsState;

        // A scope loaded again: the old lifetime may end after the new one started, one pair of listeners in any case.
        _TelemetryBrowserErrorsStop();
        state.callback = callback;

        state.onError = function (event) {
            var error = event.error;
            telemetryBrowserErrorsReport(event.message || (error && error.message) || 'Script error', (error && error.stack) || '');
        };

        // What was rejected may be anything, a getter that throws too. Only an error or a string says something.
        state.onRejection = function (event) {
            var message = 'Unhandled rejection';
            var stack = '';

            try {
                var reason = event.reason;

                if (typeof reason === 'string') {
                    message = reason;
                } else if (reason && typeof reason.message === 'string') {
                    message = reason.message;
                    stack = reason.stack || '';
                }
            } catch (error) {
                message = 'Unhandled rejection, not readable';
                stack = '';
            }

            telemetryBrowserErrorsReport(message, stack);
        };

        window.addEventListener('error', state.onError);
        window.addEventListener('unhandledrejection', state.onRejection);
    },

    TelemetryBrowserErrorsStop: function () {
        var state = telemetryBrowserErrorsState;

        if (state.onError === null)
            return;

        window.removeEventListener('error', state.onError);
        window.removeEventListener('unhandledrejection', state.onRejection);
        state.onError = null;
        state.onRejection = null;
        state.callback = null;
    }
};

autoAddDeps(LibraryTelemetryBrowserErrors, '$telemetryBrowserErrorsState');
mergeInto(LibraryManager.library, LibraryTelemetryBrowserErrors);
