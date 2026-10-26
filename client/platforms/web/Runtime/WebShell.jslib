// Bridge to window.webShell of the WebGL template (TemplateData/web-shell.js). Every template has to load it.
mergeInto(LibraryManager.library,
    {
        WebShellSafeAreaRevision: function () {
            if (window.webShell === undefined)
                throw new Error("[WebShell] web-shell.js is not loaded: the WebGL template has to load TemplateData/web-shell.js");

            return window.webShell.safeArea.revision();
        },

        // side: 0 left, 1 top, 2 right, 3 bottom; a fraction of the canvas width (left, right) or height (top, bottom).
        WebShellSafeAreaInset: function (side) {
            if (window.webShell === undefined)
                throw new Error("[WebShell] web-shell.js is not loaded: the WebGL template has to load TemplateData/web-shell.js");

            const insets = window.webShell.safeArea.normalized;

            switch (side) {
                case 0: return insets.left;
                case 1: return insets.top;
                case 2: return insets.right;
                case 3: return insets.bottom;
            }

            throw new Error("[WebShell] unknown safe area side " + side);
        },

        WebShellIsMobile: function () {
            if (window.webShell === undefined)
                throw new Error("[WebShell] web-shell.js is not loaded: the WebGL template has to load TemplateData/web-shell.js");

            return window.webShell.isMobile ? 1 : 0;
        }
    });
