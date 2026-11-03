// The visibility of the page on node: the jslib is loaded with the page around it replaced by stubs.
// Run by TelemetryPageTests (EditMode), or by hand: node --test Tests~/telemetry-page.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Web', 'TelemetryPage.jslib'), 'utf8');

// What the preprocessor of emscripten makes of it: a call of the pointer.
const jslib = source.replace(/\{\{\{\s*makeDynCall\('[a-z]+',\s*'([^']+)'\)\s*\}\}\}/g, 'dynCall($1)');

function load({ audio = true, resizeObserver = true } = {}) {
    const library = {};
    const documentListeners = new Map();
    const windowListeners = new Map();
    const document = { hidden: false, fullscreenElement: null, addEventListener: (name, listener) => documentListeners.set(name, listener) };
    let becameHidden = 0;
    const events = [];
    const resizes = [];
    const canvas = { clientWidth: 800, clientHeight: 600, width: 1600, height: 1200 };
    const timers = new Map();
    let nextTimer = 1;
    const observers = [];
    const audioListeners = new Map();
    const audioContext = { state: 'suspended', onstatechange: 'unity', addEventListener: (name, listener) => audioListeners.set(name, listener) };

    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        autoAddDeps: () => { },
        document,
        window: { addEventListener: (name, listener) => windowListeners.set(name, listener) },
        dynCall: handler => handler,
        Module: { canvas },
        setTimeout: (callback, ms) => { timers.set(nextTimer, { callback, ms }); return nextTimer++; },
        clearTimeout: id => timers.delete(id),
        ResizeObserver: resizeObserver ? class { constructor(callback) { this.callback = callback; observers.push(this); } observe(target) { this.target = target; } } : undefined,
        WEBAudio: audio ? { audioContext } : undefined
    });

    vm.runInContext(jslib, context);
    context.telemetryPageState = library.$telemetryPageState;
    context.telemetryPageEvents = library.$telemetryPageEvents;
    // Emscripten puts a value of a $ entry into framework.js as code: `var telemetryPageResizeMs = <value>;`.
    vm.runInContext('var telemetryPageResizeMs = ' + library.$telemetryPageResizeMs + ';', context);
    library.TelemetryPageListen(() => becameHidden++, kind => events.push(kind), (...size) => resizes.push(size));

    return {
        library,
        document,
        documentListeners,
        windowListeners,
        becameHidden: () => becameHidden,
        events,
        resizes,
        canvas,
        timers,
        observers,
        audioContext,
        audioListeners,
        runTimers: () => {
            const pending = [...timers.values()];
            timers.clear();
            pending.forEach(timer => timer.callback());
        }
    };
}

test('a hidden tab fires once and is seen as hidden until it is back', () => {
    const page = load();

    page.document.hidden = true;
    page.documentListeners.get('visibilitychange')();
    page.documentListeners.get('visibilitychange')();

    assert.strictEqual(page.becameHidden(), 1);
    assert.strictEqual(page.library.TelemetryPageIsHidden(), true);

    page.document.hidden = false;
    page.documentListeners.get('visibilitychange')();

    assert.strictEqual(page.becameHidden(), 1);
    assert.strictEqual(page.library.TelemetryPageIsHidden(), false);
});

test('a page going away counts as hidden, a page back from the cache as what it is', () => {
    const page = load();

    page.windowListeners.get('pagehide')();
    assert.strictEqual(page.becameHidden(), 1);
    assert.strictEqual(page.library.TelemetryPageIsHidden(), true);

    page.windowListeners.get('pageshow')({ persisted: true });
    assert.strictEqual(page.library.TelemetryPageIsHidden(), false);
});

test('a usual page show changes nothing', () => {
    const page = load();

    page.windowListeners.get('pagehide')();
    page.windowListeners.get('pageshow')({ persisted: false });

    assert.strictEqual(page.library.TelemetryPageIsHidden(), true);
});

test('a second listen keeps the listeners and takes the new callback', () => {
    const page = load();
    const listener = page.documentListeners.get('visibilitychange');
    let second = 0;

    page.library.TelemetryPageListen(() => second++, () => { }, () => { });
    assert.strictEqual(page.documentListeners.get('visibilitychange'), listener);

    page.document.hidden = true;
    listener();

    assert.strictEqual(second, 1);
    assert.strictEqual(page.becameHidden(), 0);
});

// The numbers of TelemetryPageEvent.
test('every event of the life of the page goes to the managed side with its number', () => {
    const page = load();

    page.document.hidden = true;
    page.documentListeners.get('visibilitychange')();
    page.document.hidden = false;
    page.documentListeners.get('visibilitychange')();
    page.windowListeners.get('pagehide')({ persisted: false });
    page.windowListeners.get('pagehide')({ persisted: true });
    page.windowListeners.get('pageshow')({ persisted: true });
    page.windowListeners.get('pageshow')({ persisted: false });
    page.documentListeners.get('freeze')();
    page.documentListeners.get('resume')();

    assert.deepStrictEqual(page.events, [0, 1, 2, 3, 4, 5, 6]);
});

test('one listener per event: the hidden state and the events share it', () => {
    const page = load();

    assert.deepStrictEqual([...page.documentListeners.keys()].sort(),
        ['freeze', 'fullscreenchange', 'resume', 'visibilitychange', 'webkitfullscreenchange']);
    assert.deepStrictEqual([...page.windowListeners.keys()].sort(), ['pagehide', 'pageshow', 'resize']);
});

test('the page only listens: nothing of the network, only the two functions', () => {
    assert.ok(!/navigator\.|fetch\(|XMLHttpRequest/.test(source));
    const functions = Object.keys(load().library).filter(name => name.startsWith('TelemetryPage') && !name.endsWith('__deps'));
    assert.deepStrictEqual(functions.sort(), ['TelemetryPageIsHidden', 'TelemetryPageListen']);
});

test('fullscreen: an enter and an exit, once each, by either name of the event', () => {
    const page = load();

    page.document.fullscreenElement = {};
    page.documentListeners.get('fullscreenchange')();
    page.documentListeners.get('webkitfullscreenchange')();
    page.document.fullscreenElement = null;
    page.documentListeners.get('webkitfullscreenchange')();

    assert.deepStrictEqual(page.events, [7, 8]);
});

test('a resize is reported once it holds, with the size of the canvas and of its buffer', () => {
    const page = load();
    const observer = page.observers[0];

    assert.strictEqual(observer.target, page.canvas);

    for (let i = 0; i < 20; i++)
        observer.callback();

    assert.strictEqual(page.timers.size, 1);
    assert.strictEqual([...page.timers.values()][0].ms, 500);

    page.canvas.clientWidth = 390;
    page.canvas.clientHeight = 844;
    page.canvas.width = 390;
    page.canvas.height = 844;
    page.runTimers();

    assert.deepStrictEqual(page.resizes, [[390, 844, 390, 844]]);
});

test('a resize back to the last size is none', () => {
    const page = load();

    page.observers[0].callback();
    page.runTimers();

    assert.deepStrictEqual(page.resizes, []);
});

test('without ResizeObserver the resize of the window', () => {
    const page = load({ resizeObserver: false });

    page.windowListeners.get('resize')();
    page.canvas.width = 10;
    page.runTimers();

    assert.deepStrictEqual(page.resizes, [[800, 600, 10, 1200]]);
});

test('the states of the AudioContext of Unity, its own handler kept', () => {
    const page = load();
    const listener = page.audioListeners.get('statechange');

    for (const state of ['running', 'suspended', 'interrupted', 'closed', 'something'])
    {
        page.audioContext.state = state;
        listener();
    }

    assert.deepStrictEqual(page.events, [9, 10, 11, 12, 13]);
    assert.strictEqual(page.audioContext.onstatechange, 'unity');
});

test('no audio of Unity is no listener', () => {
    assert.doesNotThrow(() => load({ audio: false }));
});
