mergeInto(LibraryManager.library,
    {
        WebLoadingScreenShow: function () {
            window.loadingScreen.show();
        },

        WebLoadingScreenShowInstantly: function () {
            window.loadingScreen.showInstantly();
        },

        WebLoadingScreenHide: function () {
            window.loadingScreen.hide();
        },

        WebLoadingScreenIsShown: function () {
            return window.loadingScreen.isShown() ? 1 : 0;
        },

        WebLoadingScreenIsHidden: function () {
            return window.loadingScreen.isHidden() ? 1 : 0;
        }
    });
