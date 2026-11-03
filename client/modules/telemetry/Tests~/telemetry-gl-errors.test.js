// The error flags of WebGL on node: the jslib is loaded with the context of Unity replaced by a stub.
// Run by TelemetryGlErrorsTests (EditMode), or by hand: node --test Tests~/telemetry-gl-errors.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const jslib = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Collectors', 'GlErrors', 'TelemetryGlErrors.jslib'), 'utf8');

function load({ codes = [], lastError = 0, context = true, cost = 0 } = {}) {
    const library = {};
    const heap = new Int32Array(64);
    const pending = [...codes];
    const clock = { now: 0 };
    const gl = { calls: 0, getError: () => { gl.calls++; clock.now += cost; return pending.length > 0 ? pending.shift() : 0; } };
    const GL = { lastError };

    const sandbox = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        HEAP32: heap,
        GL,
        GLctx: context ? gl : null,
        performance: { now: () => clock.now }
    });

    vm.runInContext(jslib, sandbox);
    sandbox.telemetryGlErrorsState = library.$telemetryGlErrorsState;

    return {
        gl,
        GL,
        lastMs: () => library.TelemetryGlErrorsLastMs(),
        take: max => {
            const count = library.TelemetryGlErrorsTake(16, max);
            return [...heap.slice(4, 4 + count)];
        }
    };
}

test('the codes of the context until NO_ERROR, in their order', () => {
    const page = load({ codes: [0x0505, 0x0502] });

    assert.deepStrictEqual(page.take(8), [0x0505, 0x0502]);
    assert.deepStrictEqual(page.take(8), []);
});

test('the take is bounded', () => {
    const page = load({ codes: [1, 2, 3, 4, 5] });

    assert.deepStrictEqual(page.take(3), [1, 2, 3]);
    assert.strictEqual(page.gl.calls, 3);
});

test('the error recorded by emscripten comes after, once', () => {
    const page = load({ codes: [0x0502], lastError: 0x0500 });

    assert.deepStrictEqual(page.take(8), [0x0502, 0x0500]);
    assert.strictEqual(page.GL.lastError, 0);
    assert.deepStrictEqual(page.take(8), []);
});

test('no context is no error', () => {
    assert.deepStrictEqual(load({ context: false }).take(8), []);
});

test('the time of the loop of a take is kept for the sample', () => {
    const page = load({ codes: [0x0505], cost: 4 });

    page.take(8);

    assert.strictEqual(page.lastMs(), 8);
});
