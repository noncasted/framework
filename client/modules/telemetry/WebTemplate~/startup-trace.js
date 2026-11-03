// The trace of the page before the build, read by the client telemetry when it sends the startup trace. Put by the build into the
// <head> of index.html, first (TelemetryStartupTraceInjection): the template of the game calls it from its own scripts.
//
//   startupTrace.begin('Unity loader');  ...  startupTrace.end('Unity loader');
//   startupTrace.mark('Loading wheel shown');
//
// Names are code names without digits (a digit reads as an id on the server). A span begun while another is open is its child;
// end closes the latest open span of that name. Nothing here throws into the page, and the trace stays bounded.
(function () {
    'use strict';

    if (window.startupTrace)
        return;

    var maxSpans = 256;
    var spans = [];
    var open = [];

    function now() {
        return performance.now();
    }

    function guarded(action) {
        return function (name) {
            try {
                action(String(name));
            } catch (error) {
            }
        };
    }

    function add(name, start, end) {
        if (spans.length >= maxSpans)
            return -1;

        spans.push({ name: name, parent: open.length > 0 ? open[open.length - 1] : -1, start: start, end: end });
        return spans.length - 1;
    }

    try {
        // The files of the start must stay in the buffer: by default it holds 250 entries.
        if (performance.setResourceTimingBufferSize)
            performance.setResourceTimingBufferSize(1000);
    } catch (error) {
    }

    window.startupTrace = {
        begin: guarded(function (name) {
            var index = add(name, now(), -1);

            if (index >= 0)
                open.push(index);
        }),

        end: guarded(function (name) {
            for (var i = open.length - 1; i >= 0; i--) {
                if (spans[open[i]].name !== name)
                    continue;

                spans[open[i]].end = now();
                open.splice(i, 1);
                return;
            }
        }),

        mark: guarded(function (name) {
            var time = now();
            add(name, time, time);
        }),

        // A copy: the reader may keep it.
        read: function () {
            return spans.map(function (span) {
                return { name: span.name, parent: span.parent, start: span.start, end: span.end };
            });
        }
    };
})();
