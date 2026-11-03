var LibraryTelemetryTrace = {
    // One key of the local storage of the origin. A storage that throws (a private window, a full quota, storage off) keeps
    // nothing: the trace is a hint, the game goes on.
    $telemetryTraceKey: 'framework.telemetry.session',

    TelemetryTraceRead__deps: ['$telemetryTraceKey'],
    TelemetryTraceRead: function () {
        var text = null;

        try {
            text = window.localStorage.getItem(telemetryTraceKey);
        } catch (error) {
        }

        if (text === null)
            return 0;

        var size = lengthBytesUTF8(text) + 1;
        var pointer = _malloc(size);
        stringToUTF8(text, pointer, size);

        return pointer;
    },

    TelemetryTraceWrite__deps: ['$telemetryTraceKey'],
    TelemetryTraceWrite: function (text) {
        try {
            window.localStorage.setItem(telemetryTraceKey, UTF8ToString(text));
        } catch (error) {
        }
    },

    TelemetryTraceClear__deps: ['$telemetryTraceKey'],
    TelemetryTraceClear: function () {
        try {
            window.localStorage.removeItem(telemetryTraceKey);
        } catch (error) {
        }
    }
};

mergeInto(LibraryManager.library, LibraryTelemetryTrace);
