// The trace of the open session in the local storage, on node: the jslib is loaded with the page around it replaced by stubs.
// Run by TelemetrySessionTraceTests (EditMode), or by hand: node --test Tests~/telemetry-trace.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const jslib = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Sessions', 'Trace', 'TelemetryTrace.jslib'), 'utf8');

function load(storage) {
    const library = {};
    const strings = new Map();
    let nextPointer = 16;

    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        window: { localStorage: storage },
        lengthBytesUTF8: text => Buffer.byteLength(text),
        _malloc: () => (nextPointer += 16),
        stringToUTF8: (text, pointer) => strings.set(pointer, text),
        UTF8ToString: pointer => pointer
    });

    vm.runInContext(jslib, context);
    context.telemetryTraceKey = library.$telemetryTraceKey;

    return { library, read: () => strings.get(library.TelemetryTraceRead()) };
}

function memoryStorage() {
    const items = new Map();

    return {
        items,
        getItem: key => (items.has(key) ? items.get(key) : null),
        setItem: (key, value) => items.set(key, value),
        removeItem: key => items.delete(key)
    };
}

function throwingStorage() {
    const fail = () => { throw new Error('SecurityError'); };
    return { getItem: fail, setItem: fail, removeItem: fail };
}

test('a written trace is read back under one key, and cleared', () => {
    const storage = memoryStorage();
    const page = load(storage);

    page.library.TelemetryTraceWrite('{"id":"a"}');

    assert.strictEqual(page.read(), '{"id":"a"}');
    assert.deepStrictEqual([...storage.items.keys()], ['framework.telemetry.session']);

    page.library.TelemetryTraceClear();
    assert.strictEqual(page.library.TelemetryTraceRead(), 0);
});

test('nothing kept is the null pointer', () => {
    assert.strictEqual(load(memoryStorage()).library.TelemetryTraceRead(), 0);
});

test('a storage that throws keeps nothing and breaks nothing', () => {
    const page = load(throwingStorage());

    page.library.TelemetryTraceWrite('{"id":"a"}');
    page.library.TelemetryTraceClear();

    assert.strictEqual(page.library.TelemetryTraceRead(), 0);
});

test('only the local storage: nothing of the network', () => {
    assert.ok(!/navigator\.|fetch\(|XMLHttpRequest|sessionStorage|document\.cookie/.test(jslib));
});
