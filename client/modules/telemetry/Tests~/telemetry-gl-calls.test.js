// The calls of the context of Unity on node: the jslib is loaded with the context replaced by a stub, $ entries as emscripten puts
// them into framework.js (`var name = <value>;`). Run by TelemetryGlCallsTests (EditMode), or by hand:
// node --test Tests~/telemetry-gl-calls.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const jslib = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Collectors', 'GlCalls', 'TelemetryGlCalls.jslib'), 'utf8');

// A prototype of the contexts of the browser and its calls.
function stubPrototype(clock, costs = {}) {
    const calls = [];
    const proto = {};

    for (const name of ['compileShader', 'getShaderParameter', 'getShaderInfoLog', 'linkProgram', 'getProgramParameter',
        'getProgramInfoLog', 'bufferData', 'bufferSubData', 'texImage2D', 'texSubImage2D', 'texImage3D', 'texSubImage3D',
        'compressedTexImage2D', 'compressedTexSubImage2D', 'compressedTexImage3D', 'compressedTexSubImage3D', 'drawArrays']) {
        proto[name] = function (...args) {
            calls.push({ name, args, self: this });
            clock.now += costs[name] || 0;

            if (name === 'getShaderParameter')
                return true;

            if (name === 'drawArrays' && args[0] === 'throw')
                throw new Error('lost');

            return name + '.result';
        };
    }

    return { proto, calls };
}

function load({ context = true, costs } = {}) {
    const library = {};
    const clock = { now: 0 };
    const heap = new Float64Array(64);
    const stub = stubPrototype(clock, costs);
    const WebGL2RenderingContext = function () { };
    WebGL2RenderingContext.prototype = stub.proto;

    const sandbox = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        HEAPF64: heap,
        performance: { now: () => clock.now },
        WebGL2RenderingContext
    });

    vm.runInContext(jslib, sandbox);

    for (const key of Object.keys(library).filter(key => key.startsWith('$') && !key.endsWith('__deps'))) {
        const value = library[key];
        vm.runInContext(`var ${key.slice(1)} = ${typeof value === 'function' ? value.toString() : JSON.stringify(value)};`, sandbox);
    }

    // What framework.js runs when it starts, before the context exists.
    vm.runInContext(library.TelemetryGlCallsInstall__postset, sandbox);
    sandbox.GLctx = context ? new WebGL2RenderingContext() : null;

    return {
        library,
        sandbox,
        stub,
        gl: () => sandbox.GLctx,
        take: (maxSlow = 8) => {
            heap.fill(-1);
            const slow = library.TelemetryGlCallsTake(0, maxSlow);
            const at = [...heap.slice(0, 8)];
            const slowCalls = [];

            for (let i = 0; i < slow; i++)
                slowCalls.push([heap[8 + i * 2], heap[9 + i * 2]]);

            return {
                compiles: at[0], compileMs: at[1], links: at[2], linkMs: at[3],
                bufferBytes: at[4], bufferDataCalls: at[5], textureBytes: at[6], textureUploads: at[7], slow: slowCalls
            };
        }
    };
}

test('the $ entries are values emscripten can put as code', () => {
    const library = {};
    vm.runInContext(jslib, vm.createContext({ LibraryManager: { library }, mergeInto: (target, added) => Object.assign(target, added) }));

    for (const key of Object.keys(library).filter(key => key.startsWith('$') && typeof library[key] !== 'function' && !key.endsWith('__deps')))
        assert.doesNotThrow(() => new vm.Script(`var x = ${JSON.stringify(library[key])};`), key);
});

test('compiles and links are counted and timed, the status queries in their time', () => {
    const page = load({ costs: { compileShader: 2, getShaderParameter: 3, linkProgram: 4, getProgramParameter: 30, getProgramInfoLog: 1 } });
    page.library.TelemetryGlCallsInstall(50);
    const gl = page.gl();

    gl.compileShader('vs');
    gl.compileShader('fs');
    assert.strictEqual(gl.getShaderParameter('vs', 0x8B81), true);
    gl.linkProgram('p');
    gl.getProgramParameter('p', 0x8B82);
    gl.getProgramInfoLog('p');

    const taken = page.take();
    assert.strictEqual(taken.compiles, 2);
    assert.strictEqual(taken.compileMs, 7);
    assert.strictEqual(taken.links, 1);
    assert.strictEqual(taken.linkMs, 35);
    assert.deepStrictEqual(taken.slow, []);

    const again = page.take();
    assert.strictEqual(again.compiles, 0);
    assert.strictEqual(again.linkMs, 0);
});

test('a call over the threshold is a slow one with its kind, bounded per take', () => {
    const page = load({ costs: { compileShader: 60, getShaderParameter: 80, linkProgram: 10, getProgramParameter: 200 } });
    page.library.TelemetryGlCallsInstall(50);
    const gl = page.gl();

    gl.compileShader('fs');
    gl.getShaderParameter('fs', 0x8B81);
    gl.linkProgram('p');
    gl.getProgramParameter('p', 0x8B82);

    assert.deepStrictEqual(page.take().slow, [[0, 60], [1, 80], [3, 200]]);

    for (let i = 0; i < 12; i++)
        gl.compileShader('fs');

    assert.strictEqual(page.take(4).slow.length, 4);
});

test('the wrappers pass the arguments, the context and the result, and an exception goes on', () => {
    const page = load();
    page.library.TelemetryGlCallsInstall(50);
    const gl = page.gl();

    assert.strictEqual(gl.linkProgram('p'), 'linkProgram.result');
    assert.strictEqual(gl.bufferData(1, 16, 2), 'bufferData.result');
    assert.deepStrictEqual(page.stub.calls.map(call => [call.name, call.args]), [['linkProgram', ['p']], ['bufferData', [1, 16, 2]]]);
    assert.ok(page.stub.calls.every(call => call.self === gl));
    assert.throws(() => gl.drawArrays('throw'), /lost/);
});

test('buffers: the bytes of the data, a bufferData is a new store', () => {
    const page = load();
    page.library.TelemetryGlCallsInstall(50);
    const gl = page.gl();
    const heap = new Uint8Array(1000);

    gl.bufferData(1, heap, 2, 100, 64);
    gl.bufferData(1, 256, 2);
    gl.bufferSubData(1, 0, heap, 10, 32);
    gl.bufferSubData(1, 0, new Float32Array(8));

    const taken = page.take();
    assert.strictEqual(taken.bufferDataCalls, 2);
    assert.strictEqual(taken.bufferBytes, 64 + 32 + 32);
});

test('textures: the bytes by the size, format and type, compressed by the data', () => {
    const page = load();
    page.library.TelemetryGlCallsInstall(50);
    const gl = page.gl();
    const heap = new Uint8Array(100000);

    gl.texImage2D(0x0DE1, 0, 0x8058, 16, 8, 0, 0x1908, 0x1401, heap, 0);
    gl.texImage2D(0x0DE1, 0, 0x8058, 16, 8, 0, 0x1908, 0x1401, null);
    gl.texSubImage2D(0x0DE1, 0, 0, 0, 4, 4, 0x1903, 0x1406, heap, 0);
    gl.texImage2D(0x0DE1, 0, 0x1908, 0x1908, 0x1401, { width: 10, height: 10 });
    gl.compressedTexImage2D(0x0DE1, 0, 0x93B0, 16, 16, 0, heap, 50, 256);
    gl.compressedTexSubImage2D(0x0DE1, 0, 0, 0, 16, 16, 0x93B0, 128, 0);

    const taken = page.take();
    assert.strictEqual(taken.textureUploads, 5);
    assert.strictEqual(taken.textureBytes, 16 * 8 * 4 + 4 * 4 * 4 + 10 * 10 * 4 + 256 + 128);
});

test('the prototype is wrapped once, a new context of Unity is counted, another canvas is not', () => {
    const page = load();
    vm.runInContext(page.library.TelemetryGlCallsInstall__postset, page.sandbox);
    page.library.TelemetryGlCallsInstall(50);

    page.gl().compileShader('a');
    assert.strictEqual(page.stub.calls.length, 1);

    const other = new page.sandbox.WebGL2RenderingContext();
    other.compileShader('other');
    other.bufferData(1, new Uint8Array(64), 2);
    assert.strictEqual(page.stub.calls.length, 3);

    page.sandbox.GLctx = new page.sandbox.WebGL2RenderingContext();
    page.gl().compileShader('b');

    const taken = page.take();
    assert.strictEqual(taken.compiles, 2);
    assert.strictEqual(taken.bufferBytes, 0);
});

test('no context breaks nothing', () => {
    const page = load({ context: false });
    page.library.TelemetryGlCallsInstall(50);

    assert.strictEqual(page.take().compiles, 0);
});

test('nothing of the network or the page', () => {
    assert.ok(!/navigator\.|fetch\(|XMLHttpRequest|localStorage|document\./.test(jslib));
});
