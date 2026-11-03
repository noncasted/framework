mergeInto(LibraryManager.library, {
    // By the index of ITelemetryWebGlInfo: 0 version, 1 max texture size, 2 antialias, 3 preserveDrawingBuffer, 4 powerPreference,
    // 5 the timer query. -1 is not known. Numbers only: no name of the GPU or of the driver here.
    TelemetryWebGlRead: function (what) {
        try {
            if (typeof GLctx === 'undefined' || !GLctx || GLctx.isContextLost())
                return -1;

            const isWebGl2 = typeof WebGL2RenderingContext !== 'undefined' && GLctx instanceof WebGL2RenderingContext;
            const attributes = GLctx.getContextAttributes();

            switch (what) {
                case 0:
                    return isWebGl2 ? 2 : 1;
                case 1:
                    return GLctx.getParameter(GLctx.MAX_TEXTURE_SIZE) | 0;
                case 2:
                    return attributes === null ? -1 : (attributes.antialias ? 1 : 0);
                case 3:
                    return attributes === null ? -1 : (attributes.preserveDrawingBuffer ? 1 : 0);
                case 4: {
                    const preference = attributes === null ? undefined : attributes.powerPreference;
                    return preference === 'default' ? 0 : preference === 'high-performance' ? 1 : preference === 'low-power' ? 2 : -1;
                }
                case 5:
                    return isWebGl2 && GLctx.getSupportedExtensions().indexOf('EXT_disjoint_timer_query_webgl2') !== -1 ? 1 : 0;
                default:
                    return -1;
            }
        } catch (error) {
            return -1;
        }
    }
});
