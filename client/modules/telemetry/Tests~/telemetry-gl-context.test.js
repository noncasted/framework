// The events of the WebGL context on node: the jslib is loaded with the page around it replaced by stubs.
// Run by TelemetryGlContextTests (EditMode), or by hand: node --test Tests~/telemetry-gl-context.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Collectors', 'GlContext', 'TelemetryGlContext.jslib'), 'utf8');

// What the preprocessor of emscripten makes of it: a call of the pointer.
const jslib = source.replace(/\{\{\{\s*makeDynCall\('[a-z]+',\s*'([^']+)'\)\s*\}\}\}/g, 'dynCall($1)');

function load(GLctx) {
    const library = {};
    const listeners = new Map();

    const canvas = {
        addEventListener: (name, listener) => listeners.set(name, [...(listeners.get(name) || []), listener])
    };

    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        autoAddDeps: () => { },
        Module: { canvas },
        GLctx,
        dynCall: handler => handler
    });

    vm.runInContext(jslib, context);
    context.telemetryGlContextState = library.$telemetryGlContextState;
    context.telemetryGlContextNotify = library.$telemetryGlContextNotify;

    return {
        library,
        listeners,
        fire: (name, event = {}) => (listeners.get(name) || []).forEach(listener => listener(event))
    };
}

test('the events of the canvas are counted and the state follows them', () => {
    const page = load();
    page.library.TelemetryGlContextListen(() => { });
    assert.strictEqual(page.library.TelemetryGlContextIsLost(), false);

    page.fire('webglcontextlost');
    assert.strictEqual(page.library.TelemetryGlContextIsLost(), true);

    page.fire('webglcontextrestored');
    assert.strictEqual(page.library.TelemetryGlContextIsLost(), false);

    page.fire('webglcontextlost');

    assert.strictEqual(page.library.TelemetryGlContextTakeLost(), 2);
    assert.strictEqual(page.library.TelemetryGlContextTakeRestored(), 1);
});

test('a take empties its counter, the state stays', () => {
    const page = load();
    page.library.TelemetryGlContextListen(() => { });

    page.fire('webglcontextlost');
    page.library.TelemetryGlContextTakeLost();

    assert.strictEqual(page.library.TelemetryGlContextTakeLost(), 0);
    assert.strictEqual(page.library.TelemetryGlContextIsLost(), true);
});

test('every event calls the managed side once', () => {
    const page = load();
    let calls = 0;
    page.library.TelemetryGlContextListen(() => calls++);

    page.fire('webglcontextlost');
    page.fire('webglcontextrestored');

    assert.strictEqual(calls, 2);
});

test('the counters are there for a callback of the same event', () => {
    const page = load();
    let taken = -1;
    page.library.TelemetryGlContextListen(() => { taken = page.library.TelemetryGlContextTakeLost(); });

    page.fire('webglcontextlost');

    assert.strictEqual(taken, 1);
});

test('a second listen keeps one pair of listeners and takes the new callback', () => {
    const page = load();
    let first = 0;
    let second = 0;

    page.library.TelemetryGlContextListen(() => first++);
    page.library.TelemetryGlContextListen(() => second++);

    assert.strictEqual(page.listeners.get('webglcontextlost').length, 1);
    assert.strictEqual(page.listeners.get('webglcontextrestored').length, 1);

    page.fire('webglcontextlost');

    assert.strictEqual(page.library.TelemetryGlContextTakeLost(), 1);
    assert.strictEqual(first, 0);
    assert.strictEqual(second, 1);
});

test('a second listen keeps what is not taken', () => {
    const page = load();
    page.library.TelemetryGlContextListen(() => { });

    page.fire('webglcontextlost');
    page.library.TelemetryGlContextListen(() => { });

    assert.strictEqual(page.library.TelemetryGlContextTakeLost(), 1);
    assert.strictEqual(page.library.TelemetryGlContextIsLost(), true);
});

// The default action of the event is what the browser does with a lost context: the behaviour of the game stays as it is.
test('the event is only counted: no default is prevented, nothing of it is read', () => {
    const page = load();
    const reads = [];
    const event = new Proxy({}, { get: (target, key) => { reads.push(key); return undefined; } });
    page.library.TelemetryGlContextListen(() => { });

    page.fire('webglcontextlost', event);
    page.fire('webglcontextrestored', event);

    assert.deepStrictEqual(reads, []);
    assert.ok(!source.includes('preventDefault'));
});

test('a dead wasm stays silent for the page and the counters go on', () => {
    const page = load();
    let calls = 0;

    page.library.TelemetryGlContextListen(() => {
        calls++;
        throw new Error('a dead wasm');
    });

    assert.doesNotThrow(() => page.fire('webglcontextlost'));
    assert.doesNotThrow(() => page.fire('webglcontextlost'));

    assert.strictEqual(calls, 1);
    assert.strictEqual(page.library.TelemetryGlContextTakeLost(), 2);
});

test('the script only listens: nothing of the page or the device is read', () => {
    const code = source.replace(/\/\/.*$/gm, '');

    for (const fragment of ['navigator', 'location', 'document', 'fetch(', 'XMLHttpRequest', 'getParameter', 'getExtension', 'localStorage'])
        assert.ok(!code.includes(fragment), fragment);
});

test('a context lost without the event reads as lost', () => {
    const page = load({ isContextLost: () => true });
    page.library.TelemetryGlContextListen(() => { });

    assert.strictEqual(page.library.TelemetryGlContextIsLost(), true);
    assert.strictEqual(page.library.TelemetryGlContextTakeLost(), 0);
});
