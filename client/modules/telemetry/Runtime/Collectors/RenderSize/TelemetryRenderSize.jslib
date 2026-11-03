mergeInto(LibraryManager.library, {
    // A lost context has a buffer of 0x0: no value then.
    TelemetryRenderBufferWidth: function () {
        return typeof GLctx === 'undefined' || !GLctx ? -1 : GLctx.drawingBufferWidth;
    },

    TelemetryRenderBufferHeight: function () {
        return typeof GLctx === 'undefined' || !GLctx ? -1 : GLctx.drawingBufferHeight;
    },

    TelemetryRenderCanvasWidth: function () {
        return Module.canvas ? Math.round(Module.canvas.clientWidth) : -1;
    },

    TelemetryRenderCanvasHeight: function () {
        return Module.canvas ? Math.round(Module.canvas.clientHeight) : -1;
    },

    // The ratio Unity applies (lib/SystemInfo.js of the engine): devicePixelRatio of the config of the loader, else of the window;
    // 1 when the page sizes the buffer itself.
    TelemetryRenderDpr: function () {
        if (Module.matchWebGLToCanvasSize === false)
            return 1;

        return Module.devicePixelRatio || window.devicePixelRatio || 1;
    }
});
