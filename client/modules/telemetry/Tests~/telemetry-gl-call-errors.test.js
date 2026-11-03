// The errors of the calls of WebGL on node: the jslib is loaded with GL of emscripten and the context replaced by stubs, $ entries as
// emscripten puts them into framework.js (`var name = <value>;`). Run by TelemetryGlCallErrorsTests (EditMode), or by hand:
// node --test Tests~/telemetry-gl-call-errors.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const jslib = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Collectors', 'GlCallErrors', 'TelemetryGlCallErrors.jslib'), 'utf8');

// A prototype of the contexts with a queue of error flags: a call named in `fails` raises its code.
function stubPrototype(fails) {
    const proto = {
        pending: [],
        getError() { return this.pending.length > 0 ? this.pending.shift() : 0; }
    };

    for (const name of ['bindTexture', 'drawArrays', 'getParameter']) {
        proto[name] = function (...args) {
            if (fails[name])
                this.pending.push(fails[name]);

            return name + '.result';
        };
    }

    Object.defineProperty(proto, 'drawingBufferWidth', { get() { return 640; } });
    return proto;
}

function load({ search = '', fails = {} } = {}) {
    const library = {};
    const heap = new Int32Array(64);
    const memory = new Map();
    const frames = [];
    let next = 100;
    const recorded = [];
    const GL = { lastError: 0, recordError(code) { recorded.push(code); if (!GL.lastError) GL.lastError = code; } };
    const WebGL2RenderingContext = function () { };
    WebGL2RenderingContext.prototype = stubPrototype(fails);

    const sandbox = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        HEAP32: heap,
        GL,
        WebGL2RenderingContext,
        URLSearchParams,
        location: { search },
        performance: { now: () => 1234.56 },
        requestAnimationFrame: callback => frames.push(callback),
        lengthBytesUTF8: text => text.length,
        _malloc: () => next++,
        stringToUTF8: (text, pointer) => memory.set(pointer, text)
    });

    vm.runInContext(jslib, sandbox);

    for (const key of Object.keys(library).filter(key => key.startsWith('$') && !key.endsWith('__deps'))) {
        const value = library[key];
        vm.runInContext(`var ${key.slice(1)} = ${typeof value === 'function' ? value.toString() : JSON.stringify(value)};`, sandbox);
    }

    // What framework.js runs when it starts, before the context exists.
    vm.runInContext(library.TelemetryGlCallErrorsTake__postset, sandbox);
    sandbox.GLctx = new WebGL2RenderingContext();
    sandbox.GLctx.pending = [];

    return {
        sandbox,
        GL,
        recorded,
        gl: () => sandbox.GLctx,
        frame: () => frames.shift()(),
        // emscripten's own gl* function, as framework.js names it.
        emscriptenCall: vm.runInContext('(function _glGetIntegerv() { GL.recordError(0x500); })', sandbox),
        take: () => {
            const lines = library.TelemetryGlCallErrorsTake(0);
            const counts = { recorded: heap[0], checked: heap[1], isChecking: heap[2] };
            const pointer = lines > 0 ? library.TelemetryGlCallErrorsReadDetails() : 0;
            return { counts, lines: pointer ? memory.get(pointer).split('\n').map(line => line.split('\t')) : [] };
        }
    };
}

test('the $ entries are values emscripten can put as code', () => {
    const library = {};
    vm.runInContext(jslib, vm.createContext({ LibraryManager: { library }, mergeInto: (target, added) => Object.assign(target, added) }));

    for (const key of Object.keys(library).filter(key => key.startsWith('$') && typeof library[key] !== 'function' && !key.endsWith('__deps')))
        assert.doesNotThrow(() => new vm.Script(`var x = ${JSON.stringify(library[key])};`), key);
});

test('an error of emscripten is counted with its gl* function, once per code and call, and still recorded', () => {
    const page = load();

    page.emscriptenCall();
    page.emscriptenCall();

    const taken = page.take();
    assert.deepStrictEqual(taken.counts, { recorded: 2, checked: 0, isChecking: 0 });
    assert.deepStrictEqual(taken.lines, [['emscripten', '1280', 'glGetIntegerv', '', '-1', '1234.6']]);
    assert.deepStrictEqual(page.recorded, [0x500, 0x500]);
    assert.strictEqual(page.GL.lastError, 0x500);

    page.emscriptenCall();
    assert.deepStrictEqual(page.take(), { counts: { recorded: 1, checked: 0, isChecking: 0 }, lines: [] });
});

test('without the parameter nothing of the context is wrapped', () => {
    const page = load({ search: '?glcheck=0', fails: { bindTexture: 0x502 } });

    assert.strictEqual(Object.prototype.hasOwnProperty.call(page.gl().constructor.prototype, 'telemetryGlCallErrorsChecked'), false);
    page.gl().bindTexture(0x1234, null);

    assert.strictEqual(page.gl().getError(), 0x502, 'the flag stays for the take of the sample');
    assert.deepStrictEqual(page.take().counts, { recorded: 0, checked: 0, isChecking: 0 });
});

test('the check gives the call, its arguments and the frame, and hands the code to the take of the flags', () => {
    const page = load({ search: '?x=1&glcheck', fails: { bindTexture: 0x502 } });
    const gl = page.gl();

    page.frame();
    page.frame();
    assert.strictEqual(gl.bindTexture(0x1234, null), 'bindTexture.result');
    gl.bindTexture(0xDE1, { constructor: { name: 'WebGLTexture' } });
    gl.drawArrays(4, 0, 6);

    const taken = page.take();
    assert.deepStrictEqual(taken.counts, { recorded: 0, checked: 2, isChecking: 1 });
    assert.deepStrictEqual(taken.lines, [['check', '1282', 'bindTexture', '0x1234,null', '2', '1234.6']]);
    assert.deepStrictEqual([...page.sandbox.telemetryGlCallErrorsState.codes], [0x502]);
    assert.strictEqual(gl.getError(), 0, 'the flag is taken by the check');
    assert.strictEqual(gl.drawingBufferWidth, 640, 'the accessors stay as they were');
});

test('the arguments are short', () => {
    const page = load();
    const text = page.sandbox.telemetryGlCallErrorsArguments([3, 2.5, true, 'a'.repeat(40), new Float32Array(16), undefined]);

    assert.strictEqual(text, '3,2.5,true,"aaaaaaaaaaaaaaaa...",Float32Array(16),undefined');
    assert.ok(page.sandbox.telemetryGlCallErrorsArguments(new Array(100).fill(0x8D40)).length <= 99);
});

test('the first errors of the page are bounded', () => {
    const page = load();

    for (let i = 0; i < 50; i++)
        vm.runInContext(`(function _glCall${i}() { GL.recordError(0x502); })()`, page.sandbox);

    const taken = page.take();
    assert.strictEqual(taken.counts.recorded, 50);
    assert.strictEqual(taken.lines.length, 32);
});

test('the caller of another engine: name@url', () => {
    const page = load();
    const original = page.sandbox.Error;
    page.sandbox.Error = function () { return { stack: 'GL.recordError@x.js:1:2\n_glUniform4f@x.js:3:4\n@wasm' }; };

    assert.strictEqual(page.sandbox.telemetryGlCallErrorsCaller(), 'glUniform4f');
    page.sandbox.Error = original;
});
