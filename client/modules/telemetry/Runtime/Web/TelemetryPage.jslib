var LibraryTelemetryPage = {
    $telemetryPageState: {
        isListening: false,
        isHidden: false,
        onBecameHidden: null
    },

    // A scope loaded again only takes the callback: the listeners of the page stay.
    TelemetryPageListen: function (onBecameHidden) {
        telemetryPageState.onBecameHidden = onBecameHidden;

        if (telemetryPageState.isListening)
            return;

        telemetryPageState.isListening = true;
        telemetryPageState.isHidden = document.hidden;

        function setHidden(isHidden) {
            if (telemetryPageState.isHidden === isHidden)
                return;

            telemetryPageState.isHidden = isHidden;

            if (isHidden)
                {{{ makeDynCall('v', 'telemetryPageState.onBecameHidden') }}}();
        }

        document.addEventListener('visibilitychange', function () {
            setHidden(document.hidden);
        });

        // Not "unload": it would take the page out of the back-forward cache. A page that goes there is away for a while
        // whatever visibilitychange says (Safari has been late with it).
        window.addEventListener('pagehide', function () {
            setHidden(true);
        });

        window.addEventListener('pageshow', function (event) {
            if (event.persisted)
                setHidden(document.hidden);
        });
    },

    TelemetryPageIsHidden: function () {
        return telemetryPageState.isHidden;
    }
};

autoAddDeps(LibraryTelemetryPage, '$telemetryPageState');
mergeInto(LibraryManager.library, LibraryTelemetryPage);
