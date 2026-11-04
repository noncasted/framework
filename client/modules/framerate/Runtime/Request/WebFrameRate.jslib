// ?fps= of the page for WebFrameRateRequest.cs: a frame rate limit stronger than the settings of the game (A/B checks on
// production). native lifts the limit (-1), <n> sets it on any device, absent leaves it to the game (0: the automatic rate on
// a phone, see FrameRateLimit.cs). The game reads it once at start; a bad value throws.
mergeInto(LibraryManager.library, {
    WebFrameRateQuery: function () {
        const requested = new URLSearchParams(window.location.search).get("fps");

        if (requested === null)
            return 0;

        if (requested === "native")
            return -1;

        const limit = Number(requested);

        if (Number.isInteger(limit) === false || limit <= 0)
            throw new Error("[WebFrameRate] ?fps must be native or a positive integer, got " + requested);

        return limit;
    }
});
