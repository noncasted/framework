// GL probe of the stand: run.mjs injects it before any script of the page (Page.addScriptToEvaluateOnNewDocument),
// after `window.__GP_CFG = {errMs}`. It wraps every method of WebGL2RenderingContext.prototype. All in window.__gp:
//  errMs        until this page time (ms), gl.getError after every call; a non-zero code is kept with the call, the last
//               12 calls and the stack (errs, at most 50).
//  count(ms)    counts the calls by name over the GL frames (rAF callbacks that made GL calls) of a window of ms;
//               the result in done = {c: {name: calls}, frames, ms}.
//  startRec(n)  records every call (name and short arguments) of the next n GL frames into recs (an array per frame).
//  emErrs       the errors emscripten records itself (GL.recordError, never seen by getError): window.__glrec(code,
//               stack) is called by framework.js when run.mjs patches it (--hook-record-error).
//  glFrames     GL frames since the start of the page (the prof step of run.mjs takes its frames from it).
//  info()       the renderer of the context and the size of its drawing buffer.
// Standalone it also works in any page: paste it into the console and call __gp.count(5000).
(function () {
    var cfg = window.__GP_CFG || {};
    var P = window.__gp = { c: null, frames: 0, glFrames: 0, frameCalls: 0, errs: [], emErrs: [], recs: [], rec: null, recLeft: 0, last: [], ids: 0, ctx: null };
    var proto = WebGL2RenderingContext.prototype;
    var origGetError = proto.getError;
    var errMs = cfg.errMs || 0;

    function fmt(a) {
        if (a === null || a === undefined) return String(a);
        if (typeof a === 'number') return Math.round(a * 1e4) / 1e4;
        if (typeof a === 'string') return a.slice(0, 40);
        if (typeof a === 'boolean') return a;
        if (ArrayBuffer.isView(a)) return a.constructor.name + '[' + a.length + ']';
        if (typeof a === 'object') return (a.constructor && a.constructor.name || 'o').replace('WebGL', '') + (a.__id || (a.__id = ++P.ids));
        return typeof a;
    }

    function describe(name, args) {
        return name + '(' + Array.prototype.map.call(args, fmt).join(',') + ')';
    }

    function stack() {
        return (new Error().stack || '').split('\n').slice(2, 30).join('\n');
    }

    Object.getOwnPropertyNames(proto).forEach(function (name) {
        var d = Object.getOwnPropertyDescriptor(proto, name);
        if (!d || typeof d.value !== 'function' || name === 'constructor') return;
        var orig = d.value;
        proto[name] = function () {
            P.ctx = this;
            P.frameCalls++;
            if (P.c) P.c[name] = (P.c[name] || 0) + 1;
            if (P.rec) P.rec.push(describe(name, arguments));
            var r = orig.apply(this, arguments);
            if (errMs && name !== 'getError' && performance.now() < errMs) {
                var desc = describe(name, arguments);
                P.last.push(desc);
                if (P.last.length > 12) P.last.shift();
                var e = origGetError.call(this);
                if (e && P.errs.length < 50)
                    P.errs.push({ t: Math.round(performance.now()), call: desc, code: e, last: P.last.slice(), stack: stack() });
            }
            return r;
        };
    });

    window.__glrec = function (code, errorStack) {
        if (P.emErrs.length < 50)
            P.emErrs.push({ t: Math.round(performance.now()), code: code, last: P.last.slice(), stack: (errorStack || '').split('\n').slice(1, 30).join('\n') });
    };

    // A GL frame is a rAF callback that made GL calls.
    var raf = window.requestAnimationFrame.bind(window);
    window.requestAnimationFrame = function (cb) {
        return raf(function (t) {
            var before = P.frameCalls;
            cb(t);
            if (P.frameCalls > before) {
                P.glFrames++;
                if (P.c) P.frames++;
                if (P.rec) {
                    P.recs.push(P.rec);
                    P.rec = --P.recLeft > 0 ? [] : null;
                }
            }
        });
    };

    P.count = function (ms) {
        P.c = {};
        P.frames = 0;
        P.done = null;
        P.t0 = performance.now();
        setTimeout(function () {
            P.done = { c: P.c, frames: P.frames, ms: performance.now() - P.t0 };
            P.c = null;
        }, ms);
    };

    // The record starts at the next rAF, so it holds whole GL frames.
    P.startRec = function (frames) {
        P.recLeft = frames || 1;
        raf(function () { P.rec = []; });
    };

    P.info = function () {
        if (!P.ctx) return null;
        var gl = P.ctx;
        var x = gl.getExtension('WEBGL_debug_renderer_info');
        return { renderer: x ? gl.getParameter(x.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER), buffer: gl.drawingBufferWidth + 'x' + gl.drawingBufferHeight };
    };
})();
