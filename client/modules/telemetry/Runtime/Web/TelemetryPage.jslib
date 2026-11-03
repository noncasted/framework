var LibraryTelemetryPage = {
    $telemetryPageState: {
        isListening: false,
        isHidden: false,
        onBecameHidden: null,
        onLifecycle: null
    },

    // The numbers of TelemetryPageEvent.
    $telemetryPageEvents: {
        hidden: 0,
        visible: 1,
        pageHide: 2,
        pageHidePersisted: 3,
        pageShowPersisted: 4,
        freeze: 5,
        resume: 6
    },

    // A scope loaded again only takes the callbacks: the listeners of the page stay.
    TelemetryPageListen__deps: ['$telemetryPageEvents'],
    TelemetryPageListen: function (onBecameHidden, onLifecycle) {
        telemetryPageState.onBecameHidden = onBecameHidden;
        telemetryPageState.onLifecycle = onLifecycle;

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
    },

    TelemetryPageIsHidden: function () {
        return telemetryPageState.isHidden;
    }
};

autoAddDeps(LibraryTelemetryPage, '$telemetryPageState');
mergeInto(LibraryManager.library, LibraryTelemetryPage);
