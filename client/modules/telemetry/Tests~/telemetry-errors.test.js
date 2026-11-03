// window.onerror and unhandledrejection on node: the jslib is loaded with the page around it replaced by stubs.
// Run by TelemetryBrowserErrorsTests (EditMode), or by hand: node --test Tests~/telemetry-errors.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Errors', 'Web', 'TelemetryBrowserErrors.jslib'), 'utf8');

// What the preprocessor of emscripten makes of it: a call of the pointer.
const jslib = source.replace(/\{\{\{\s*makeDynCall\('[a-z]+',\s*'([^']+)'\)\s*\}\}\}/g, 'dynCall($1)');

function load({ now = () => 0, callback } = {}) {
    const library = {};
    const listeners = { error: new Set(), unhandledrejection: new Set() };
    const memory = new Map();
    const reports = [];
    let nextPointer = 16;
    let live = 0;

    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        autoAddDeps: () => { },
        window: {
            addEventListener: (name, listener) => listeners[name].add(listener),
            removeEventListener: (name, listener) => listeners[name].delete(listener)
        },
        Date: { now },
        lengthBytesUTF8: text => Buffer.byteLength(text),
        _malloc: () => {
            live++;
            return (nextPointer += 16);
        },
        _free: pointer => {
            live--;
            memory.delete(pointer);
        },
        stringToUTF8: (text, pointer) => memory.set(pointer, text),
        dynCall: handler => handler
    });

    vm.runInContext(jslib, context);
    context.telemetryBrowserErrorsState = library.$telemetryBrowserErrorsState;
    context.telemetryBrowserErrorsReport = library.$telemetryBrowserErrorsReport;
    context._TelemetryBrowserErrorsStop = library.TelemetryBrowserErrorsStop;

    const managed = callback || ((message, stack) => reports.push({ message: memory.get(message), stack: memory.get(stack) }));

    return {
        library,
        listeners,
        reports,
        liveAllocations: () => live,
        listen: () => library.TelemetryBrowserErrorsListen(managed),
        fire: (name, event) => [...listeners[name]].forEach(listener => listener(event))
    };
}

// Every read of the event is on the record: what is not read cannot leave the page.
function recordedEvent(fields) {
    const reads = new Set();
    return { reads, event: new Proxy(fields, { get: (target, key) => { reads.add(key); return target[key]; } }) };
}

test('an error goes with the text and the stack', () => {
    const page = load();
    page.listen();

    page.fire('error', { message: 'Uncaught TypeError: x', error: { message: 'x', stack: 'at f (https://game.example/a.js:1:2)' } });

    assert.deepStrictEqual(page.reports, [{ message: 'Uncaught TypeError: x', stack: 'at f (https://game.example/a.js:1:2)' }]);
});

test('the file name and the place of an error are not even read', () => {
    const page = load();
    const { reads, event } = recordedEvent({ message: 'm', filename: 'https://game.example/?user=1', lineno: 1, colno: 2, error: null });
    page.listen();

    page.fire('error', event);

    for (const field of ['filename', 'lineno', 'colno', 'target', 'srcElement', 'currentTarget'])
        assert.ok(!reads.has(field), field);

    assert.strictEqual(page.reports[0].message, 'm');
});

test('the script reads nothing about the page', () => {
    for (const fragment of ['location', 'document', 'href', 'navigator', 'filename', 'referrer'])
        assert.ok(!source.replace(/\/\/.*$/gm, '').includes(fragment), fragment);
});

test('an error of another origin has no error object', () => {
    const page = load();
    page.listen();

    page.fire('error', { message: 'Script error.', error: null });

    assert.deepStrictEqual(page.reports, [{ message: 'Script error.', stack: '' }]);
});

const rejections = [
    ['error', { message: 'bad', stack: 'at g' }, 'bad', 'at g'],
    ['string', 'bad text', 'bad text', ''],
    ['object', { user: 'Ivan' }, 'Unhandled rejection', ''],
    ['number', 12345, 'Unhandled rejection', ''],
    ['nothing', undefined, 'Unhandled rejection', ''],
    ['null', null, 'Unhandled rejection', '']
];

for (const [title, reason, message, stack] of rejections) {
    test(`a rejection with ${title}`, () => {
        const page = load();
        page.listen();

        page.fire('unhandledrejection', { reason });

        assert.deepStrictEqual(page.reports, [{ message, stack }]);
    });
}

test('a rejection with a getter that throws stays silent for the page', () => {
    const page = load();
    page.listen();

    page.fire('unhandledrejection', { reason: { get message() { throw new Error('no'); } } });

    assert.strictEqual(page.reports[0].message, 'Unhandled rejection, not readable');
});

test('texts are cut to the limits of the contract', () => {
    const page = load();
    page.listen();

    page.fire('error', { message: 'm'.repeat(5000), error: { stack: 's'.repeat(20000) } });

    assert.strictEqual(page.reports[0].message.length, 2048);
    assert.strictEqual(page.reports[0].stack.length, 8192);
});

test('a loop of errors is bounded per second and comes back', () => {
    let time = 5000;
    const page = load({ now: () => time });
    page.listen();

    for (let i = 0; i < 1000; i++)
        page.fire('error', { message: 'loop', error: null });

    assert.strictEqual(page.reports.length, 16);

    time += 1000;
    page.fire('error', { message: 'loop', error: null });

    assert.strictEqual(page.reports.length, 17);
});

test('the memory of every call is given back', () => {
    const page = load();
    page.listen();

    page.fire('error', { message: 'one', error: null });
    page.fire('unhandledrejection', { reason: 'two' });

    assert.strictEqual(page.liveAllocations(), 0);
});

test('an error inside the managed side does not come back and stops the listening', () => {
    let calls = 0;
    let page;

    page = load({
        callback: () => {
            calls++;
            page.fire('error', { message: 'inside', error: null });
            throw new Error('a dead wasm');
        }
    });

    page.listen();

    assert.doesNotThrow(() => page.fire('error', { message: 'first', error: null }));
    assert.strictEqual(calls, 1);
    assert.strictEqual(page.listeners.error.size, 0);
    assert.strictEqual(page.listeners.unhandledrejection.size, 0);
    assert.strictEqual(page.liveAllocations(), 0);
});

test('after the stop there are no listeners', () => {
    const page = load();
    page.listen();
    assert.strictEqual(page.listeners.error.size, 1);
    assert.strictEqual(page.listeners.unhandledrejection.size, 1);

    page.library.TelemetryBrowserErrorsStop();

    assert.strictEqual(page.listeners.error.size, 0);
    assert.strictEqual(page.listeners.unhandledrejection.size, 0);
});

test('a second listen keeps one pair of listeners', () => {
    const page = load();

    page.listen();
    page.listen();

    assert.strictEqual(page.listeners.error.size, 1);
    assert.strictEqual(page.listeners.unhandledrejection.size, 1);
});
