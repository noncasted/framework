// The render pixel ratio of a phone or a tablet (window.renderResolution), put first into the <head> of the built index.html by
// RenderResolutionInjection of the resolution module. The template of the game, on a mobile page only (a desktop keeps the ratio
// of the browser):
//     config.devicePixelRatio = renderResolution.pixelRatio(canvas, MOBILE_MAX_PIXEL_RATIO);   // before createUnityInstance
//     renderResolution.follow(unityInstance.Module, canvas, MOBILE_MAX_PIXEL_RATIO);           // once the instance runs
//
// The ratio is device / k, k whole, so each canvas pixel is exactly k device pixels and a pixelated upscale stays even.
// - high renders at the device ratio (k 1: a 2.75x Android of 895x393 CSS pixels renders 2460x1080);
// - low takes k up to device / maxRatio, as the largest k that still leaves the visible short side (the canvas, in device
//   pixels) at least 360 (maxRatio 1 there: 820x360);
// - auto is high on Android and the other phones, low on iOS and iPadOS (an iPad 7 is GPU bound already at 1080x740, an
//   iPhone 11 too).
// The resolution setting of the game (WebRenderResolution.jslib) calls set(mode) once its settings are loaded, the engine is
// running by then; the page keeps the last mode so that the next start creates the canvas at it, before the first frame (the
// save of the game stays the source). The canvas grows when the page enters fullscreen (the address bar of Chrome on Android
// goes away after the first tap, Unity starts before it): follow picks k again on every resize and hands it to the running
// engine (Module.devicePixelRatio is read every frame).
// Diagnostics, by the address only, stronger than the setting: ?dpr=native renders at the device pixel ratio, ?dpr=<number>
// replaces maxRatio of the low resolution. Nothing changes without them.
(function () {
    "use strict";

    const ua = navigator.userAgent;
    // iPadOS reports itself as a Mac: it is told apart by its touch points.
    const isIPadOS = /Macintosh/.test(ua) && navigator.maxTouchPoints > 1;
    const isIOS = /iPhone|iPad|iPod/i.test(ua) || isIPadOS;

    const query = new URLSearchParams(window.location.search);

    const modes = ["auto", "low", "high"];
    // The key of the web shell this came from (window.webShell of a template): a mode picked before the move is kept.
    const storageKey = "webShell.resolution";
    let mode = storedMode();

    // The engine module of the running instance (unityInstance.Module), its canvas and the maxRatio it renders with.
    let engine = null;
    let engineCanvas = null;
    let engineMaxRatio = 0;

    function storedMode() {
        let stored = null;

        // Storage is refused in some private modes and with blocked site data: the start takes auto then.
        try {
            stored = window.localStorage.getItem(storageKey);
        } catch (error) {
            console.info("[renderResolution] local storage: " + error);
        }

        if (stored === null)
            return "auto";
        if (modes.includes(stored) === false)
            throw new Error("[renderResolution] unknown stored resolution " + stored);

        return stored;
    }

    function set(next) {
        if (modes.includes(next) === false)
            throw new Error("[renderResolution] the resolution must be one of " + modes.join(", ") + ", got " + next);

        mode = next;

        try {
            window.localStorage.setItem(storageKey, next);
        } catch (error) {
            console.info("[renderResolution] local storage: " + error);
        }

        refresh();
    }

    function isHigh() {
        return mode === "high" || (mode === "auto" && isIOS === false);
    }

    function pixelRatio(canvas, maxRatio) {
        const device = window.devicePixelRatio || 1;
        const requested = query.get("dpr");

        if (requested === "native")
            return device;
        if (requested !== null)
            maxRatio = Number(requested);
        else if (isHigh() === true)
            return device;
        if (Number.isFinite(maxRatio) === false || maxRatio <= 0)
            throw new Error("[renderResolution] the max render pixel ratio must be a positive number, got " + maxRatio);

        const shortSide = Math.min(screen.width, screen.height) * device;
        // Browser bars (Safari on a phone has no fullscreen) take some of the screen: the canvas is the visible part. Its CSS
        // size is fractional (1080 device pixels at 2.75 are 392.73 CSS pixels), the epsilons keep exact quotients whole.
        const rect = canvas.getBoundingClientRect();
        const visibleSide = Math.min(shortSide, Math.min(rect.width, rect.height) * device);
        const limited = Math.min(Math.ceil(device / maxRatio - 1e-6), Math.floor(visibleSide / 360 + 1e-6));

        return device / Math.max(1, limited);
    }

    function refresh() {
        if (engine === null)
            return;

        const ratio = pixelRatio(engineCanvas, engineMaxRatio);

        if (engine.devicePixelRatio !== ratio)
            engine.devicePixelRatio = ratio;
    }

    // iOS updates innerWidth a moment after orientationchange.
    function refreshLater() {
        refresh();
        setTimeout(refresh, 100);
        setTimeout(refresh, 500);
    }

    function follow(module, canvas, maxRatio) {
        if (engine !== null)
            throw new Error("[renderResolution] follow is called twice");

        engine = module;
        engineCanvas = canvas;
        engineMaxRatio = maxRatio;

        window.addEventListener("resize", refresh);
        window.addEventListener("orientationchange", refreshLater);
        document.addEventListener("fullscreenchange", refreshLater);
        document.addEventListener("webkitfullscreenchange", refreshLater);

        if (window.visualViewport !== undefined)
            window.visualViewport.addEventListener("resize", refresh);

        new ResizeObserver(refresh).observe(canvas);

        refresh();
    }

    window.renderResolution = {
        pixelRatio: pixelRatio,
        follow: follow,
        // Called by WebRenderResolution.jslib with the resolution setting of the game: auto, low or high.
        set: set,
        mode: () => mode,
    };
})();
