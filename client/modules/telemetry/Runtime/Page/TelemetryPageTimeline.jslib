var LibraryTelemetryPageTimeline = {
    // The page before the build as JSON for TelemetryPageTimeline: the navigation, the files and the spans of window.startupTrace.
    // A file goes by the last segment of its path: the host, the query and the hash never leave the page.
    TelemetryPageTimelineRead: function () {
        function number(value) {
            return typeof value === 'number' && isFinite(value) && value > 0 ? value : 0;
        }

        // The scheme and the host go first: an address without a path must not leave its host as the name.
        function fileName(address) {
            var path = String(address).replace(/^[a-z][a-z0-9+.\-]*:\/\/[^\/?#]*/i, '').split('#')[0].split('?')[0];
            return path.substring(path.lastIndexOf('/') + 1);
        }

        function readNavigation() {
            var entries = performance.getEntriesByType ? performance.getEntriesByType('navigation') : [];
            var entry = entries.length > 0 ? entries[0] : {};
            var fields = ['redirectStart', 'redirectEnd', 'domainLookupStart', 'domainLookupEnd', 'connectStart', 'connectEnd',
                'requestStart', 'responseStart', 'responseEnd', 'domContentLoadedEventEnd', 'loadEventEnd'];
            var navigation = {};

            for (var i = 0; i < fields.length; i++)
                navigation[fields[i]] = number(entry[fields[i]]);

            return navigation;
        }

        function readFiles() {
            var entries = performance.getEntriesByType ? performance.getEntriesByType('resource') : [];
            var files = [];

            for (var i = 0; i < entries.length; i++) {
                var entry = entries[i];

                files.push({
                    name: fileName(entry.name),
                    start: number(entry.startTime),
                    end: number(entry.responseEnd),
                    transfer: number(entry.transferSize),
                    encoded: number(entry.encodedBodySize),
                    decoded: number(entry.decodedBodySize)
                });
            }

            return files;
        }

        function readSpans() {
            var trace = window.startupTrace;
            return trace && typeof trace.read === 'function' ? trace.read() : [];
        }

        var text;

        try {
            text = JSON.stringify({ now: performance.now(), navigation: readNavigation(), files: readFiles(), spans: readSpans() });
        } catch (error) {
            text = JSON.stringify({ now: performance.now(), navigation: {}, files: [], spans: [] });
        }

        var size = lengthBytesUTF8(text) + 1;
        var pointer = _malloc(size);
        stringToUTF8(text, pointer, size);

        return pointer;
    }
};

mergeInto(LibraryManager.library, LibraryTelemetryPageTimeline);
