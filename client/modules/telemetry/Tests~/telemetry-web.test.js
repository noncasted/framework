// The browser side of the module, on node: the jslib is loaded with the page around it replaced by stubs.
// Run by TelemetryWebJsTests (EditMode), or by hand: node --test Tests~/telemetry-web.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const jslib = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Collectors', 'Web', 'TelemetryWeb.jslib'), 'utf8');

const closedList = ['browser', 'browserMajor', 'os', 'osMajor', 'gpu', 'cores', 'memoryClass', 'screenWidth', 'screenHeight', 'dpr'];
const readableNavigator = ['userAgent', 'hardwareConcurrency', 'deviceMemory', 'maxTouchPoints'];

const chromeAndroid = 'Mozilla/5.0 (Linux; Android 14; Pixel 7 Build/UP1A.231005.007) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.6723.58 Mobile Safari/537.36';
const telegramAndroid = 'Mozilla/5.0 (Linux; Android 13; SM-G991B Build/TP1A.220624.014; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/129.0.6668.100 Mobile Safari/537.36 Telegram-Android/11.2.2 (Samsung SM-G991B; Android 13; SDK 33; HIGH)';
const webViewAndroid = 'Mozilla/5.0 (Linux; Android 12; SM-A525F Build/SP1A.210812.016; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/128.0.6613.88 Mobile Safari/537.36';
const safariIos = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_1_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1.2 Mobile/15E148 Safari/604.1';
const webViewIos = 'Mozilla/5.0 (iPhone; CPU iPhone OS 16_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148';
const chromeIos = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0.6723.37 Mobile/15E148 Safari/604.1';
const firefoxAndroid = 'Mozilla/5.0 (Android 14; Mobile; rv:131.0) Gecko/131.0 Firefox/131.0';
const firefoxLinux = 'Mozilla/5.0 (X11; Linux x86_64; rv:132.0) Gecko/20100101 Firefox/132.0';
const samsung = 'Mozilla/5.0 (Linux; Android 13; SAMSUNG SM-S911B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/25.0 Chrome/121.0.0.0 Mobile Safari/537.36';
const edgeWindows = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.2849.56';
// iPadOS by default: the user agent of a Mac, with touch points; Telegram on iOS does not name itself, its bridge does.
const safariIpad = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15';
const webViewIpad = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko)';
const safariIpadMobile = 'Mozilla/5.0 (iPad; CPU OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1';
const safariMac = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15';

function canvasWith(renderer, debugRenderer) {
    const gl = {
        RENDERER: 7937,
        lost: false,
        getExtension: name => {
            if (name === 'WEBGL_debug_renderer_info')
                return debugRenderer === undefined ? null : { UNMASKED_RENDERER_WEBGL: 37446 };

            if (name === 'WEBGL_lose_context')
                return { loseContext: () => { gl.lost = true; } };

            return null;
        },
        getParameter: id => (id === 37446 ? debugRenderer : renderer)
    };

    return { gl, getContext: () => (renderer === undefined ? null : gl) };
}

function load({ userAgent = chromeAndroid, cores = 8, deviceMemory = 4, canvas = canvasWith('WebKit WebGL', 'Adreno (TM) 730'), memory, heap = 1024, maxTouchPoints = 0, bridge } = {}) {
    const library = {};
    const reads = new Set();
    const strings = new Map();
    const created = { count: 0 };
    let nextPointer = 16;

    // Null is a field the browser does not have.
    const fields = { userAgent, hardwareConcurrency: cores ?? undefined, deviceMemory: deviceMemory ?? undefined, maxTouchPoints };

    const navigator = new Proxy(fields, {
        get: (target, key) => {
            reads.add(key);
            return target[key];
        }
    });

    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, source) => Object.assign(target, source),
        navigator,
        window: bridge === undefined ? { devicePixelRatio: 2.625 } : { devicePixelRatio: 2.625, [bridge]: {} },
        screen: { width: 412, height: 915 },
        document: { createElement: () => { created.count++; return canvas; } },
        performance: memory === undefined ? {} : { memory },
        HEAP8: { length: heap },
        lengthBytesUTF8: text => Buffer.byteLength(text),
        _malloc: () => (nextPointer += 16),
        stringToUTF8: (text, pointer) => strings.set(pointer, text)
    });

    vm.runInContext(jslib, context);

    // The $ entries as emscripten puts them: globals of the module.
    for (const key of Object.keys(library).filter(key => key.startsWith('$') && !key.endsWith('__deps'))) {
        const value = library[key];
        vm.runInContext(`var ${key.slice(1)} = ${typeof value === 'function' ? value.toString() : JSON.stringify(value)};`, context);
    }

    return {
        library,
        reads,
        created,
        readDevice: () => JSON.parse(strings.get(library.TelemetryWebReadDevice())),
        readDeviceText: () => strings.get(library.TelemetryWebReadDevice())
    };
}

const userAgents = [
    ['Chrome Android', chromeAndroid, 'Chrome', 130, 'Android', 14],
    ['Telegram WebView Android', telegramAndroid, 'Telegram WebView', 129, 'Android', 13],
    ['Android WebView', webViewAndroid, 'Android WebView', 128, 'Android', 12],
    ['Safari iOS', safariIos, 'Safari', 17, 'iOS', 17],
    ['WebView iOS', webViewIos, 'iOS WebView', -1, 'iOS', 16],
    ['Chrome iOS', chromeIos, 'Chrome', 130, 'iOS', 17],
    ['Firefox Android', firefoxAndroid, 'Firefox', 131, 'Android', 14],
    ['Firefox Linux', firefoxLinux, 'Firefox', 132, 'Linux', -1],
    ['Samsung Internet', samsung, 'Samsung Internet', 25, 'Android', 13],
    ['Edge Windows', edgeWindows, 'Edge', 130, 'Windows', 10],
    ['Safari macOS', safariMac, 'Safari', 17, 'macOS', 10],
    ['Unknown', 'SomeBot/1.0', '', -1, '', -1],
    ['Empty', '', '', -1, '', -1]
];

for (const [title, userAgent, browser, browserMajor, os, osMajor] of userAgents) {
    test(`user agent: ${title}`, () => {
        const device = load({ userAgent }).readDevice();

        assert.deepStrictEqual(
            [device.browser, device.browserMajor, device.os, device.osMajor],
            [browser, browserMajor, os, osMajor]);
    });
}

const touchDevices = [
    ['Safari iPadOS as a Mac', safariIpad, 5, undefined, 'Safari', 17, 'iPadOS', -1],
    ['WebView iPadOS as a Mac', webViewIpad, 5, undefined, 'iOS WebView', -1, 'iPadOS', -1],
    ['Telegram iPadOS as a Mac', webViewIpad, 5, 'TelegramWebviewProxy', 'Telegram WebView', -1, 'iPadOS', -1],
    ['Telegram iOS', webViewIos, 5, 'TelegramWebviewProxy', 'Telegram WebView', -1, 'iOS', 16],
    ['Safari iPadOS as an iPad', safariIpadMobile, 5, undefined, 'Safari', 17, 'iPadOS', 17],
    ['Safari macOS with a touch point', safariMac, 1, undefined, 'Safari', 17, 'macOS', 10],
    ['A WebView of macOS', webViewIpad, 0, undefined, '', -1, 'macOS', 10]
];

for (const [title, userAgent, maxTouchPoints, bridge, browser, browserMajor, os, osMajor] of touchDevices) {
    test(`touch and bridge: ${title}`, () => {
        const device = load({ userAgent, maxTouchPoints, bridge }).readDevice();

        assert.deepStrictEqual(
            [device.browser, device.browserMajor, device.os, device.osMajor],
            [browser, browserMajor, os, osMajor]);
    });
}

test('the full user agent never reaches the managed side', () => {
    const fragments = ['Mozilla', 'AppleWebKit', 'Build', 'Pixel', 'SM-G991B', 'SM-A525F', 'Samsung SM', 'SDK', 'HIGH', '6668', '6723', 'UP1A', '17_1_2', '11.2.2', '15E148'];

    for (const [, userAgent] of userAgents) {
        const text = load({ userAgent, canvas: canvasWith(undefined) }).readDeviceText();

        for (const fragment of fragments)
            assert.ok(!text.includes(fragment), `'${fragment}' is in ${text}`);
    }
});

test('only the closed list is written', () => {
    assert.deepStrictEqual(Object.keys(load().readDevice()).sort(), [...closedList].sort());
});

test('the navigator is read for the closed list only', () => {
    const page = load();

    page.readDevice();

    assert.deepStrictEqual([...page.reads].filter(key => !readableNavigator.includes(key)), []);
});

test('the numbers of the device', () => {
    const device = load({ cores: 6, deviceMemory: 2 }).readDevice();

    assert.deepStrictEqual(
        [device.cores, device.memoryClass, device.screenWidth, device.screenHeight, device.dpr],
        [6, 2, 412, 915, 2.625]);
});

test('no navigator.deviceMemory and no cores are not known', () => {
    const device = load({ cores: null, deviceMemory: null }).readDevice();

    assert.deepStrictEqual([device.cores, device.memoryClass], [-1, -1]);
});

test('gpu: the model behind the debug extension, and the context is given back', () => {
    const canvas = canvasWith('WebKit WebGL', 'ANGLE (Qualcomm, Adreno (TM) 730, OpenGL ES 3.2)');

    assert.strictEqual(load({ canvas }).readDevice().gpu, 'ANGLE (Qualcomm, Adreno (TM) 730, OpenGL ES 3.2)');
    assert.strictEqual(canvas.gl.lost, true);
});

// Every session start and startup trace reads the device: a context per read would push the game's own one out of the
// limit of the page.
test('gpu: read once per page, one context for every read', () => {
    const page = load({ canvas: canvasWith('WebKit WebGL', 'Adreno (TM) 730') });

    assert.strictEqual(page.readDevice().gpu, 'Adreno (TM) 730');
    assert.strictEqual(page.readDevice().gpu, 'Adreno (TM) 730');
    assert.strictEqual(page.readDevice().gpu, 'Adreno (TM) 730');
    assert.strictEqual(page.created.count, 1);
});

test('gpu: no webgl is read once too', () => {
    const page = load({ canvas: canvasWith(undefined) });

    assert.strictEqual(page.readDevice().gpu, '');
    assert.strictEqual(page.readDevice().gpu, '');
    assert.strictEqual(page.created.count, 1);
});

test('gpu: no debug extension gives the renderer of the context', () => {
    assert.strictEqual(load({ canvas: canvasWith('Mali-G78, or similar') }).readDevice().gpu, 'Mali-G78, or similar');
});

test('gpu: no webgl is not known', () => {
    assert.strictEqual(load({ canvas: canvasWith(undefined) }).readDevice().gpu, '');
});

test('gpu: fits the form of the server, which drops a wrong value whole', () => {
    const gpu = load({ canvas: canvasWith('x', 'Mesa::Intel(R)® "UHD"' + 'a'.repeat(200)) }).readDevice().gpu;

    assert.match(gpu, /^[A-Za-z0-9 ()_.,+/\-]{1,128}$/);
    assert.ok(gpu.startsWith('Mesa'));
});

test('memory: the heap of the module', () => {
    assert.strictEqual(load({ heap: 268435456 }).library.TelemetryWebHeapBytes(), 268435456);
});

test('memory: performance.memory when there is one', () => {
    const memory = { usedJSHeapSize: 10, totalJSHeapSize: 20, jsHeapSizeLimit: 30 };
    const { library } = load({ memory });

    assert.deepStrictEqual(
        [library.TelemetryWebJsHeapUsed(), library.TelemetryWebJsHeapTotal(), library.TelemetryWebJsHeapLimit()],
        [10, 20, 30]);
});

test('memory: no performance.memory is minus one', () => {
    const { library } = load();

    assert.deepStrictEqual(
        [library.TelemetryWebJsHeapUsed(), library.TelemetryWebJsHeapTotal(), library.TelemetryWebJsHeapLimit()],
        [-1, -1, -1]);
});
