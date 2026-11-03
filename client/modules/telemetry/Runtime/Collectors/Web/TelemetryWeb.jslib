mergeInto(LibraryManager.library, {
    // The closed list of the device as JSON. The user agent stays here: only the names and the major versions leave, a
    // full one holds the model of the phone and the build of its system.
    TelemetryWebReadDevice: function () {
        function major(text, pattern) {
            const match = pattern.exec(text);
            return match === null ? -1 : parseInt(match[1], 10);
        }

        // The order matters: every Chromium has "Chrome" and "Safari", every browser on iOS has "Safari" or none.
        function readBrowser(text) {
            const chromium = /Chrome\/(\d+)/;

            if (/Telegram/.test(text))
                return { name: 'Telegram WebView', major: major(text, chromium) };

            if (/; wv\)/.test(text))
                return { name: 'Android WebView', major: major(text, chromium) };

            const known = [
                ['Edge', /Edg(?:e|A|iOS)?\/(\d+)/],
                ['Opera', /OPR\/(\d+)/],
                ['Samsung Internet', /SamsungBrowser\/(\d+)/],
                ['Yandex', /YaBrowser\/(\d+)/],
                ['Chrome', /CriOS\/(\d+)/],
                ['Firefox', /(?:Firefox|FxiOS)\/(\d+)/],
                ['Chrome', chromium],
                ['Safari', /Version\/(\d+)[.\d]* (?:Mobile\/\S+ )?Safari/]
            ];

            for (let i = 0; i < known.length; i++) {
                const version = major(text, known[i][1]);

                if (version !== -1)
                    return { name: known[i][0], major: version };
            }

            if (/(iPhone|iPad|iPod).*AppleWebKit/.test(text))
                return { name: 'iOS WebView', major: -1 };

            return { name: '', major: -1 };
        }

        function readOs(text) {
            const known = [
                ['iOS', /(?:iPhone|iPad|iPod).* OS (\d+)/, /iPhone|iPad|iPod/],
                ['Android', /Android (\d+)/, /Android/],
                ['Windows', /Windows NT (\d+)/, /Windows/],
                ['macOS', /Mac OS X (\d+)/, /Macintosh/],
                ['ChromeOS', /$^/, /CrOS/],
                ['Linux', /$^/, /Linux/]
            ];

            for (let i = 0; i < known.length; i++) {
                if (known[i][2].test(text))
                    return { name: known[i][0], major: major(text, known[i][1]) };
            }

            return { name: '', major: -1 };
        }

        // The form the server takes: a value out of it would be dropped whole.
        function readGpu() {
            const gl = document.createElement('canvas').getContext('webgl');

            if (gl === null)
                return '';

            const debug = gl.getExtension('WEBGL_debug_renderer_info');
            const renderer = debug === null ? gl.getParameter(gl.RENDERER) : gl.getParameter(debug['UNMASKED_RENDERER_WEBGL']);
            const lose = gl.getExtension('WEBGL_lose_context');

            // A page has a limited number of contexts, and the game needs its own.
            if (lose !== null)
                lose['loseContext']();

            return String(renderer).replace(/[^A-Za-z0-9 ()_.,+\/\-]/g, ' ').trim().slice(0, 128);
        }

        const userAgent = navigator['userAgent'];
        const browser = readBrowser(userAgent);
        const os = readOs(userAgent);
        const cores = navigator['hardwareConcurrency'];
        const memoryClass = navigator['deviceMemory'];

        const text = JSON.stringify({
            'browser': browser.name,
            'browserMajor': browser.major,
            'os': os.name,
            'osMajor': os.major,
            'gpu': readGpu(),
            'cores': cores === undefined ? -1 : cores,
            'memoryClass': memoryClass === undefined ? -1 : memoryClass,
            'screenWidth': screen.width,
            'screenHeight': screen.height,
            'dpr': window.devicePixelRatio
        });

        const size = lengthBytesUTF8(text) + 1;
        const pointer = _malloc(size);
        stringToUTF8(text, pointer, size);

        return pointer;
    },

    TelemetryWebHeapBytes: function () {
        return HEAP8.length;
    },

    TelemetryWebJsHeapUsed: function () {
        const memory = performance['memory'];
        return memory === undefined ? -1 : memory['usedJSHeapSize'];
    },

    TelemetryWebJsHeapTotal: function () {
        const memory = performance['memory'];
        return memory === undefined ? -1 : memory['totalJSHeapSize'];
    },

    TelemetryWebJsHeapLimit: function () {
        const memory = performance['memory'];
        return memory === undefined ? -1 : memory['jsHeapSizeLimit'];
    }
});
