// The resolution setting of the game (WebRenderPixelRatio.cs) for window.renderResolution of the page
// (WebTemplate~/render-resolution.js of the resolution module, put into the built index.html by RenderResolutionInjection).
mergeInto(LibraryManager.library, {
    // mode: the value of Global.Resolution.ResolutionMode (0 auto, 1 low, 2 high).
    WebRenderResolutionSet: function (mode) {
        if (window.renderResolution === undefined)
            throw new Error("[WebRenderResolution] render-resolution.js is not in the page: RenderResolutionInjection puts it into the built index.html");

        const names = ["auto", "low", "high"];

        if (mode < 0 || mode >= names.length)
            throw new Error("[WebRenderResolution] unknown resolution mode " + mode);

        window.renderResolution.set(names[mode]);
    }
});
