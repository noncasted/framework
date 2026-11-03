var LibraryTelemetryPage = {
    $telemetryPageState: {
        isListening: false,
        isHidden: false,
        onBecameHidden: null,
        onLifecycle: null,
        onResized: null,
        resizeTimer: 0,
        cssWidth: 0,
        cssHeight: 0,
        bufferWidth: 0,
        bufferHeight: 0
    },

    // A resize of the canvas is reported once it holds this long: a rotation, a drag of a window or a toolbar that slides fire many.
    // The buffer follows the canvas at the next frame of Unity, inside this time.
    $telemetryPageResizeMs: 500,

    // The numbers of TelemetryPageEvent.
    $telemetryPageEvents: {
        hidden: 0,
        visible: 1,
        pageHide: 2,
        pageHidePersisted: 3,
        pageShowPersisted: 4,
        freeze: 5,
        resume: 6,
        fullscreenEnter: 7,
        fullscreenExit: 8,
        audioRunning: 9,
        audioSuspended: 10,
        audioInterrupted: 11,
        audioClosed: 12,
        audioOther: 13
    },

    // A scope loaded again only takes the callbacks: the listeners of the page stay.
    TelemetryPageListen__deps: ['$telemetryPageEvents', '$telemetryPageResizeMs'],
    TelemetryPageListen: function (onBecameHidden, onLifecycle, onResized) {
        telemetryPageState.onBecameHidden = onBecameHidden;
        telemetryPageState.onLifecycle = onLifecycle;
        telemetryPageState.onResized = onResized;

        if (telemetryPageState.isListening)
            return;

        telemetryPageState.isListening = true;
        telemetryPageState.isHidden = document.hidden;

        function notify(kind) {
            {{{ makeDynCall('vi', 'telemetryPageState.onLifecycle') }}}(kind);
        }

        function setHidden(isHidden) {
            if (telemetryPageState.isHidden === isHidden)
                return;

            telemetryPageState.isHidden = isHidden;

            if (isHidden)
                {{{ makeDynCall('v', 'telemetryPageState.onBecameHidden') }}}();
        }

        document.addEventListener('visibilitychange', function () {
            notify(document.hidden ? telemetryPageEvents.hidden : telemetryPageEvents.visible);
            setHidden(document.hidden);
        });

        // Not "unload": it would take the page out of the back-forward cache. A page that goes there is away for a while
        // whatever visibilitychange says (Safari has been late with it). The event reaches the managed side while the page
        // still runs: the last moment to leave a trace of a page that is going away.
        window.addEventListener('pagehide', function (event) {
            notify(event && event.persisted ? telemetryPageEvents.pageHidePersisted : telemetryPageEvents.pageHide);
            setHidden(true);
        });

        window.addEventListener('pageshow', function (event) {
            if (!event.persisted)
                return;

            notify(telemetryPageEvents.pageShowPersisted);
            setHidden(document.hidden);
        });

        // The Page Lifecycle API of Chromium; Safari and Firefox have none.
        document.addEventListener('freeze', function () {
            notify(telemetryPageEvents.freeze);
        });

        document.addEventListener('resume', function () {
            notify(telemetryPageEvents.resume);
        });

        // Safari of iOS before 16.4 has the prefixed names only.
        var isFullscreen = !!(document.fullscreenElement || document.webkitFullscreenElement);

        function onFullscreen() {
            var now = !!(document.fullscreenElement || document.webkitFullscreenElement);

            if (now === isFullscreen)
                return;

            isFullscreen = now;
            notify(now ? telemetryPageEvents.fullscreenEnter : telemetryPageEvents.fullscreenExit);
        }

        document.addEventListener('fullscreenchange', onFullscreen);
        document.addEventListener('webkitfullscreenchange', onFullscreen);

        // The size of the canvas in CSS pixels and of its drawing buffer, never a position: the last one reported starts with the
        // size at the listen, and a resize back to it inside the wait is none.
        var canvas = Module.canvas;
        var state = telemetryPageState;

        function readSize() {
            state.cssWidth = canvas.clientWidth;
            state.cssHeight = canvas.clientHeight;
            state.bufferWidth = canvas.width;
            state.bufferHeight = canvas.height;
        }

        function reportSize() {
            state.resizeTimer = 0;

            var cssWidth = state.cssWidth, cssHeight = state.cssHeight, bufferWidth = state.bufferWidth, bufferHeight = state.bufferHeight;
            readSize();

            if (cssWidth === state.cssWidth && cssHeight === state.cssHeight && bufferWidth === state.bufferWidth && bufferHeight === state.bufferHeight)
                return;

            {{{ makeDynCall('viiii', 'telemetryPageState.onResized') }}}(state.cssWidth, state.cssHeight, state.bufferWidth, state.bufferHeight);
        }

        function onResize() {
            if (state.resizeTimer)
                clearTimeout(state.resizeTimer);

            state.resizeTimer = setTimeout(reportSize, telemetryPageResizeMs);
        }

        readSize();

        // The box of the canvas, and the window for a new devicePixelRatio: a canvas of a fixed CSS size gets a new buffer then.
        if (typeof ResizeObserver !== 'undefined')
            new ResizeObserver(onResize).observe(canvas);

        window.addEventListener('resize', onResize);

        // The AudioContext of Unity (WEBAudio of its Audio.js), made at the start of the player, before any scope. Unity has its own
        // onstatechange: a listener next to it changes nothing. Without the audio of Unity there is none.
        var audio = typeof WEBAudio !== 'undefined' && WEBAudio ? WEBAudio.audioContext : null;

        if (audio && audio.addEventListener) {
            audio.addEventListener('statechange', function () {
                switch (audio.state) {
                    case 'running': notify(telemetryPageEvents.audioRunning); break;
                    case 'suspended': notify(telemetryPageEvents.audioSuspended); break;
                    case 'interrupted': notify(telemetryPageEvents.audioInterrupted); break;
                    case 'closed': notify(telemetryPageEvents.audioClosed); break;
                    default: notify(telemetryPageEvents.audioOther);
                }
            });
        }
    },

    TelemetryPageIsHidden: function () {
        return telemetryPageState.isHidden;
    }
};

autoAddDeps(LibraryTelemetryPage, '$telemetryPageState');
mergeInto(LibraryManager.library, LibraryTelemetryPage);
