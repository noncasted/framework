mergeInto(LibraryManager.library,
    {
        WebLoadingScreenShow: function () {
            if (window.loadingScreen === undefined)
                throw new Error("[Web] window.loadingScreen is not defined: the WebGL template has no loading screen script");

            window.loadingScreen.show();
        },

        WebLoadingScreenShowInstantly: function () {
            if (window.loadingScreen === undefined)
                throw new Error("[Web] window.loadingScreen is not defined: the WebGL template has no loading screen script");

            window.loadingScreen.showInstantly();
        },

        WebLoadingScreenHide: function () {
            if (window.loadingScreen === undefined)
                throw new Error("[Web] window.loadingScreen is not defined: the WebGL template has no loading screen script");

            window.loadingScreen.hide();
        },

        WebLoadingScreenIsShown: function () {
            if (window.loadingScreen === undefined)
                throw new Error("[Web] window.loadingScreen is not defined: the WebGL template has no loading screen script");

            return window.loadingScreen.isShown() ? 1 : 0;
        },

        WebLoadingScreenIsHidden: function () {
            if (window.loadingScreen === undefined)
                throw new Error("[Web] window.loadingScreen is not defined: the WebGL template has no loading screen script");

            return window.loadingScreen.isHidden() ? 1 : 0;
        }
    });
