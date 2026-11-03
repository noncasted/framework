// The jslibs of the frame on node: the timer query of the GPU, the chain of requestAnimationFrame, the size of the render and the
// context numbers, with GLctx and the page replaced by stubs. Run by FrameTelemetryCollectorsTests (EditMode), or by hand:
// node --test Tests~/telemetry-frame.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const collectors = path.join(__dirname, '..', 'Runtime', 'Collectors');
const sources = ['Gpu/TelemetryGpuQuery.jslib', 'Raf/TelemetryRaf.jslib', 'RenderSize/TelemetryRenderSize.jslib', 'WebGlInfo/TelemetryWebGlInfo.jslib']
    .map(file => fs.readFileSync(path.join(collectors, file), 'utf8'));

const TIME_ELAPSED = 0x88BF;
const DISJOINT = 0x8FBB;

class FakeGl2 {
    constructor() {
        this.lost = false;
        this.active = null;
        this.available = false;
        this.disjoint = false;
        this.calls = [];
        this.CURRENT_QUERY = 0x8865;
        this.QUERY_RESULT = 0x8866;
        this.QUERY_RESULT_AVAILABLE = 0x8867;
        this.MAX_TEXTURE_SIZE = 0x0D33;
        this.drawingBufferWidth = 1080;
        this.drawingBufferHeight = 2400;
        this.hasTimer = true;
    }
    isContextLost() { return this.lost; }
    getExtension(name) { return name === 'EXT_disjoint_timer_query_webgl2' && this.hasTimer ? { TIME_ELAPSED_EXT: TIME_ELAPSED, GPU_DISJOINT_EXT: DISJOINT } : null; }
    getSupportedExtensions() { return this.hasTimer ? ['EXT_disjoint_timer_query_webgl2'] : []; }
    getQuery() { return this.active; }
    createQuery() { return {}; }
    isQuery(query) { return !this.lost && query !== null; }
    beginQuery(target, query) { this.calls.push('begin'); if (this.active !== null) throw new Error('INVALID_OPERATION'); this.active = query; }
    endQuery() { this.calls.push('end'); this.active = null; }
    getQueryParameter(query, name) { return name === this.QUERY_RESULT_AVAILABLE ? this.available : 4e6; }
    getParameter(name) { return name === DISJOINT ? this.disjoint : name === this.MAX_TEXTURE_SIZE ? 4096 : null; }
    getContextAttributes() { return this.lost ? null : { antialias: false, preserveDrawingBuffer: false, powerPreference: 'high-performance' }; }
}

function load(gl) {
    const library = {};
    const frames = [];
    const listeners = {};
    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        autoAddDeps: () => { },
        GLctx: gl,
        WebGL2RenderingContext: FakeGl2,
        Module: { canvas: { clientWidth: 392.7, clientHeight: 873 }, devicePixelRatio: 2.75 },
        window: { devicePixelRatio: 3, addEventListener: (name, listener) => { listeners[name] = listener; } },
        document: { hidden: false, addEventListener: (name, listener) => { listeners[name] = listener; } },
        requestAnimationFrame: callback => frames.push(callback),
        cancelAnimationFrame: () => frames.splice(0, frames.length)
    });

    sources.forEach(source => vm.runInContext(source, context));

    for (const name of Object.keys(library)) {
        if (name.startsWith('$') && typeof library[name] !== 'function')
            context[name.slice(1)] = library[name];
        else if (name.startsWith('$'))
            context[name.slice(1)] = library[name];
        else
            context['_' + name] = library[name];
    }

    const runFrame = time => frames.splice(0, frames.length).forEach(callback => callback(time));
    return { library, context, runFrame, listeners };
}

test('gpu query: begin, end, a result in a later frame', () => {
    const gl = new FakeGl2();
    const { library } = load(gl);

    assert.strictEqual(library.TelemetryGpuQueryIsAvailable(), true);
    assert.strictEqual(library.TelemetryGpuQueryBegin(), true);
    library.TelemetryGpuQueryEnd();
    assert.strictEqual(library.TelemetryGpuQueryTake(), -1);
    assert.strictEqual(library.TelemetryGpuQueryBegin(), false);

    gl.available = true;
    assert.strictEqual(library.TelemetryGpuQueryTake(), 4);
    assert.strictEqual(library.TelemetryGpuQueryBegin(), true);
});

test('gpu query: disjoint, lost context, a query of another', () => {
    const gl = new FakeGl2();
    const { library } = load(gl);

    library.TelemetryGpuQueryBegin();
    library.TelemetryGpuQueryEnd();
    gl.available = true;
    gl.disjoint = true;
    assert.strictEqual(library.TelemetryGpuQueryTake(), -2);

    gl.active = {};
    assert.strictEqual(library.TelemetryGpuQueryBegin(), false);
    gl.active = null;

    library.TelemetryGpuQueryBegin();
    gl.lost = true;
    library.TelemetryGpuQueryEnd();
    assert.strictEqual(library.TelemetryGpuQueryTake(), -2);
    assert.strictEqual(library.TelemetryGpuQueryBegin(), false);
    assert.strictEqual(library.TelemetryGpuQueryIsAvailable(), false);

    // A restored context has no query open.
    gl.lost = false;
    gl.active = null;
    gl.disjoint = false;
    assert.strictEqual(library.TelemetryGpuQueryBegin(), true);
});

test('gpu query: no extension, no query', () => {
    const gl = new FakeGl2();
    gl.hasTimer = false;
    const { library } = load(gl);

    assert.strictEqual(library.TelemetryGpuQueryIsAvailable(), false);
    assert.strictEqual(library.TelemetryGpuQueryBegin(), false);
    assert.deepStrictEqual(gl.calls, []);
});

test('raf: intervals of its own chain, none after a hide, stops', () => {
    const { library, runFrame, listeners } = load(new FakeGl2());

    library.TelemetryRafStart();
    runFrame(100);
    runFrame(116);
    runFrame(150);
    assert.strictEqual(library.TelemetryRafTakeMax(), 34);
    assert.strictEqual(library.TelemetryRafTakeAverage(), 25);
    assert.strictEqual(library.TelemetryRafTakeAverage(), -1);

    listeners['visibilitychange']();
    runFrame(5000);
    assert.strictEqual(library.TelemetryRafTakeAverage(), -1);

    library.TelemetryRafStop();
    runFrame(5016);
    assert.strictEqual(library.TelemetryRafTakeAverage(), -1);
});

test('render size: the drawing buffer, the canvas, the applied ratio', () => {
    const { library, context } = load(new FakeGl2());

    assert.strictEqual(library.TelemetryRenderBufferWidth(), 1080);
    assert.strictEqual(library.TelemetryRenderBufferHeight(), 2400);
    assert.strictEqual(library.TelemetryRenderCanvasWidth(), 393);
    assert.strictEqual(library.TelemetryRenderDpr(), 2.75);

    context.Module.matchWebGLToCanvasSize = false;
    assert.strictEqual(library.TelemetryRenderDpr(), 1);
});

test('webgl info: numbers only, -1 on a lost context', () => {
    const gl = new FakeGl2();
    const { library } = load(gl);

    assert.deepStrictEqual([0, 1, 2, 3, 4, 5].map(what => library.TelemetryWebGlRead(what)), [2, 4096, 0, 0, 1, 1]);

    gl.lost = true;
    assert.strictEqual(library.TelemetryWebGlRead(1), -1);
});
