mergeInto(LibraryManager.library,
    {
        // The whole page goes fullscreen, not the canvas: the loading cover of the template lies over the canvas
        // and has to stay visible between the scenes.
        // In Chrome and Edge the keyboard is locked in fullscreen: the browser shortcuts (Ctrl+W, Ctrl+T, Ctrl+N,
        // Esc) reach the game, a held Esc leaves fullscreen. Firefox and Safari have no Keyboard Lock API: there
        // the page is only fullscreen. The lock works only in fullscreen and is released on leaving it.
        // The call comes from a game frame, not from the DOM event handler, so it relies on the transient user
        // activation left by the click.
        // iPhone Safari has no Fullscreen API for the page (only for <video>): requestFullscreen is undefined there.
        WebPageIsFullscreenSupported: function () {
            return document.fullscreenEnabled === true
                && typeof document.documentElement.requestFullscreen === 'function';
        },

        WebPageToggleFullscreen: function () {
            if (document.fullscreenElement !== null) {
                document.exitFullscreen().catch(function (e) {
                    console.error('[WebPage] Failed to exit fullscreen: ' + e);
                });

                return;
            }

            if (window.webPageFullscreenListener === undefined) {
                window.webPageFullscreenListener = function () {
                    if (document.fullscreenElement === null && navigator.keyboard !== undefined)
                        navigator.keyboard.unlock();
                };

                document.addEventListener('fullscreenchange', window.webPageFullscreenListener);
            }

            document.documentElement.requestFullscreen().then(function () {
                if (navigator.keyboard !== undefined && navigator.keyboard.lock !== undefined)
                    return navigator.keyboard.lock();
            }).catch(function (e) {
                console.error('[WebPage] Failed to enter fullscreen: ' + e);
            });
        },

        // While enabled, closing or reloading the tab asks "Leave site?" (the text is the browser's own).
        // The browser asks only if the page has had a click or a key press, which a game always has.
        WebPageSetLeaveConfirmation: function (isEnabled) {
            if (window.webPageLeaveConfirmation === undefined) {
                window.webPageLeaveConfirmation = { isEnabled: false };

                window.addEventListener('beforeunload', function (e) {
                    if (window.webPageLeaveConfirmation.isEnabled === false)
                        return;

                    e.preventDefault();
                    e.returnValue = '';
                });
            }

            window.webPageLeaveConfirmation.isEnabled = isEnabled !== 0;
        },    });
