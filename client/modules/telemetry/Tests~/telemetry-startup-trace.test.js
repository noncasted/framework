// The trace of the page before the build on node: WebTemplate~/startup-trace.js and the reading of TelemetryPageTimeline.jslib,
// with the browser replaced by stubs. Run by TelemetryPageTimelineTests (EditMode), or by hand:
// node --test Tests~/telemetry-startup-trace.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const moduleFolder = path.join(__dirname, '..');
const script = fs.readFileSync(path.join(moduleFolder, 'WebTemplate~', 'startup-trace.js'), 'utf8');
const jslib = fs.readFileSync(path.join(moduleFolder, 'Runtime', 'Page', 'TelemetryPageTimeline.jslib'), 'utf8');

// Objects of the context of the page have its prototypes.
function plain(value) {
    return JSON.parse(JSON.stringify(value));
}

function page(entries) {
    let time = 0;
    const performance = {
        now: () => time,
        setResourceTimingBufferSize: size => { performance.bufferSize = size; },
        getEntriesByType: type => (entries && entries[type]) || []
    };
    const window = {};
    const context = vm.createContext({
        window,
        performance,
        JSON,
        String,
        isFinite,
        LibraryManager: { library: {} },
        mergeInto: (target, added) => Object.assign(target, added),
        lengthBytesUTF8: text => Buffer.byteLength(text, 'utf8'),
        _malloc: () => 0,
        stringToUTF8: text => { context.written = text; }
    });

    return {
        context,
        window,
        performance,
        at: value => { time = value; },
        runScript: () => vm.runInContext(script, context),
        read: () => {
            vm.runInContext(jslib, context);
            context.LibraryManager.library.TelemetryPageTimelineRead();
            return JSON.parse(context.written);
        }
    };
}

test('a span begun while another is open is its child, end closes the latest of the name', () => {
    const browser = page();
    browser.runScript();
    const trace = browser.window.startupTrace;

    browser.at(10); trace.begin('Unity loader');
    browser.at(20); trace.begin('Unity instance');
    browser.at(30); trace.mark('Progress half');
    browser.at(40); trace.end('Unity loader');
    browser.at(50); trace.end('Unity instance');

    assert.deepStrictEqual(plain(trace.read()), [
        { name: 'Unity loader', parent: -1, start: 10, end: 40 },
        { name: 'Unity instance', parent: 0, start: 20, end: 50 },
        { name: 'Progress half', parent: 1, start: 30, end: 30 }
    ]);
});

test('an open span stays open, an end of a span never begun changes nothing', () => {
    const browser = page();
    browser.runScript();
    const trace = browser.window.startupTrace;

    browser.at(5); trace.begin('Loading wheel');
    trace.end('Never begun');

    assert.deepStrictEqual(plain(trace.read()), [{ name: 'Loading wheel', parent: -1, start: 5, end: -1 }]);
});

test('the trace is bounded, nothing throws into the page, a second copy keeps the first', () => {
    const browser = page();
    browser.runScript();
    const trace = browser.window.startupTrace;

    for (let i = 0; i < 300; i++)
        trace.mark('Mark');

    browser.performance.now = () => { throw new Error('no clock'); };
    assert.doesNotThrow(() => trace.begin('Broken'));
    browser.runScript();

    assert.strictEqual(browser.window.startupTrace, trace);
    assert.strictEqual(trace.read().length, 256);
    assert.strictEqual(browser.performance.bufferSize, 1000);
});

test('the reading gives the navigation, the files by their names only and the spans', () => {
    const browser = page({
        navigation: [{ domainLookupStart: 1, domainLookupEnd: 2, responseEnd: 30, domContentLoadedEventEnd: 900, redirectStart: 0 }],
        resource: [
            { name: 'https://telegram.org/js/telegram-web-app.js?56', startTime: 40, responseEnd: 800, transferSize: 0, encodedBodySize: 0, decodedBodySize: 0 },
            { name: 'https://game.example.com/play/Build/Build.wasm.unityweb?v=3#x', startTime: 950, responseEnd: 4000, transferSize: 9000, encodedBodySize: 8800, decodedBodySize: 30000 },
            { name: 'https://cdn.example.com?k=secret', startTime: 960, responseEnd: 970 }
        ]
    });
    browser.runScript();
    browser.at(3); browser.window.startupTrace.begin('Unity loader');
    browser.at(7000);

    const timeline = browser.read();

    assert.strictEqual(timeline.now, 7000);
    assert.strictEqual(timeline.navigation.responseEnd, 30);
    assert.strictEqual(timeline.navigation.redirectStart, 0);
    assert.strictEqual(timeline.navigation.loadEventEnd, 0);
    assert.deepStrictEqual(timeline.files.map(file => file.name), ['telegram-web-app.js', 'Build.wasm.unityweb', '']);
    assert.strictEqual(timeline.files[1].transfer, 9000);
    assert.strictEqual(timeline.files[1].end, 4000);
    assert.deepStrictEqual(timeline.spans, [{ name: 'Unity loader', parent: -1, start: 3, end: -1 }]);
    assert.ok(!JSON.stringify(timeline).includes('example.com'));
    assert.ok(!JSON.stringify(timeline).includes('secret'));
});

test('a page without the script still gives the navigation and the files', () => {
    const browser = page({ navigation: [{ responseEnd: 10 }] });

    const timeline = browser.read();

    assert.strictEqual(timeline.navigation.responseEnd, 10);
    assert.deepStrictEqual(timeline.spans, []);
});
