var LibraryTelemetryGpuQuery = {
    // One query of TIME_ELAPSED_EXT on the context of Unity (GLctx). Every call checks the context: a lost one has no queries,
    // and a restored one needs a new object. A query of the same target open by someone else is left alone (a second begin would
    // be INVALID_OPERATION for both).
    $telemetryGpuQueryState: {
        query: null,
        context: null,
        isOpen: false,
        isPending: false
    },

    $telemetryGpuQueryExtension: function () {
        if (typeof GLctx === 'undefined' || !GLctx || GLctx.isContextLost())
            return null;

        if (typeof WebGL2RenderingContext === 'undefined' || !(GLctx instanceof WebGL2RenderingContext))
            return null;

        return GLctx.getExtension('EXT_disjoint_timer_query_webgl2');
    },

    TelemetryGpuQueryIsAvailable__deps: ['$telemetryGpuQueryExtension'],
    TelemetryGpuQueryIsAvailable: function () {
        try {
            return telemetryGpuQueryExtension() !== null;
        } catch (error) {
            return false;
        }
    },

    TelemetryGpuQueryBegin__deps: ['$telemetryGpuQueryExtension'],
    TelemetryGpuQueryBegin: function () {
        const state = telemetryGpuQueryState;

        try {
            const extension = telemetryGpuQueryExtension();

            if (extension === null || state.isOpen || state.isPending)
                return false;

            if (GLctx.getQuery(extension.TIME_ELAPSED_EXT, GLctx.CURRENT_QUERY) !== null)
                return false;

            if (state.query === null || state.context !== GLctx || !GLctx.isQuery(state.query)) {
                state.query = GLctx.createQuery();
                state.context = GLctx;
            }

            // Clears the flag of a disjoint before the measure.
            GLctx.getParameter(extension.GPU_DISJOINT_EXT);
            GLctx.beginQuery(extension.TIME_ELAPSED_EXT, state.query);
            state.isOpen = true;

            return true;
        } catch (error) {
            return false;
        }
    },

    TelemetryGpuQueryEnd__deps: ['$telemetryGpuQueryExtension'],
    TelemetryGpuQueryEnd: function () {
        const state = telemetryGpuQueryState;

        if (!state.isOpen)
            return;

        state.isOpen = false;

        try {
            const extension = telemetryGpuQueryExtension();

            if (extension === null)
                return;

            GLctx.endQuery(extension.TIME_ELAPSED_EXT);
            state.isPending = true;
        } catch (error) {
        }
    },

    // -1 not ready, -2 lost or disjoint, else milliseconds.
    TelemetryGpuQueryTake__deps: ['$telemetryGpuQueryExtension'],
    TelemetryGpuQueryTake: function () {
        const state = telemetryGpuQueryState;

        if (!state.isPending)
            return -2;

        try {
            const extension = telemetryGpuQueryExtension();

            if (extension === null || state.context !== GLctx) {
                state.isPending = false;
                return -2;
            }

            if (!GLctx.getQueryParameter(state.query, GLctx.QUERY_RESULT_AVAILABLE))
                return -1;

            state.isPending = false;
            const nanoseconds = GLctx.getQueryParameter(state.query, GLctx.QUERY_RESULT);

            if (GLctx.getParameter(extension.GPU_DISJOINT_EXT))
                return -2;

            return nanoseconds / 1e6;
        } catch (error) {
            state.isPending = false;
            return -2;
        }
    },

    TelemetryGpuQueryDrop__deps: ['TelemetryGpuQueryEnd'],
    TelemetryGpuQueryDrop: function () {
        _TelemetryGpuQueryEnd();
        telemetryGpuQueryState.isPending = false;
    }
};

autoAddDeps(LibraryTelemetryGpuQuery, '$telemetryGpuQueryState');
mergeInto(LibraryManager.library, LibraryTelemetryGpuQuery);
