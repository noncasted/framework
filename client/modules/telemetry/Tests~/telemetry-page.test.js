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

function load() {
    const library = {};
    const documentListeners = new Map();
    const windowListeners = new Map();
    const document = { hidden: false, addEventListener: (name, listener) => documentListeners.set(name, listener) };
    let becameHidden = 0;
    const events = [];

    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        autoAddDeps: () => { },
        document,
        window: { addEventListener: (name, listener) => windowListeners.set(name, listener) },
        dynCall: handler => handler
    });

    vm.runInContext(jslib, context);
    context.telemetryPageState = library.$telemetryPageState;
    context.telemetryPageEvents = library.$telemetryPageEvents;
    library.TelemetryPageListen(() => becameHidden++, kind => events.push(kind));

    return {
        library,
        document,
        documentListeners,
        windowListeners,
        becameHidden: () => becameHidden,
        events
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

    page.library.TelemetryPageListen(() => second++, () => { });
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

    assert.deepStrictEqual([...page.documentListeners.keys()].sort(), ['freeze', 'resume', 'visibilitychange']);
    assert.deepStrictEqual([...page.windowListeners.keys()].sort(), ['pagehide', 'pageshow']);
});

test('the page only listens: nothing of the network, only the two functions', () => {
    assert.ok(!/navigator\.|fetch\(|XMLHttpRequest/.test(source));
    const functions = Object.keys(load().library).filter(name => name.startsWith('TelemetryPage') && !name.endsWith('__deps'));
    assert.deepStrictEqual(functions.sort(), ['TelemetryPageIsHidden', 'TelemetryPageListen']);
});
