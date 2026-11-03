var LibraryTelemetryGlCalls = {
    // The counters of the calls of the context of Unity since the previous take. The layout of a take: TelemetryGlCallsLayout.
    // Numbers only in the $ entries: emscripten puts their values into framework.js as code.
    $telemetryGlCallsState: {
        slowMs: 50,
        maxSlow: 8,
        compiles: 0,
        compileMs: 0,
        links: 0,
        linkMs: 0,
        bufferBytes: 0,
        bufferDataCalls: 0,
        textureBytes: 0,
        textureUploads: 0,
        slowKinds: [],
        slowDurations: []
    },

    // A call of the context that took longer than the threshold. Kinds: TelemetryGlCallsLayout (compile, compile status, link, link
    // status); the status queries are where Chromium and ANGLE wait for a compilation or a link they deferred.
    $telemetryGlCallsTimed__deps: ['$telemetryGlCallsState'],
    $telemetryGlCallsTimed: function (kind, ms) {
        var state = telemetryGlCallsState;

        if (kind < 2)
            state.compileMs += ms;
        else
            state.linkMs += ms;

        if (ms >= state.slowMs && state.slowKinds.length < state.maxSlow) {
            state.slowKinds.push(kind);
            state.slowDurations.push(ms);
        }
    },

    // The bytes of the data of an upload: a view with its offset and length in elements (WebGL 2), a size in bytes, or nothing.
    $telemetryGlCallsDataBytes: function (data, offset, length) {
        if (typeof data === 'number')
            return data;

        if (!data || typeof data.byteLength !== 'number')
            return 0;

        var element = data.BYTES_PER_ELEMENT || 1;

        if (length)
            return length * element;

        return Math.max(0, data.byteLength - (offset || 0) * element);
    },

    // The bytes of an image of a texture by its size, format and type: what an upload of the CPU sends, estimated.
    $telemetryGlCallsImageBytes: function (width, height, depth, format, type) {
        var channels;

        switch (format) {
            case 0x1903: case 0x8D94: case 0x1906: case 0x1909: case 0x1902: channels = 1; break;
            case 0x8227: case 0x8228: case 0x190A: case 0x84F9: channels = 2; break;
            case 0x1907: case 0x8D98: channels = 3; break;
            default: channels = 4;
        }

        var pixel;

        switch (type) {
            case 0x1400: case 0x1401: pixel = channels; break;
            case 0x1402: case 0x1403: case 0x140B: case 0x8D61: pixel = channels * 2; break;
            case 0x1404: case 0x1405: case 0x1406: pixel = channels * 4; break;
            case 0x8363: case 0x8033: case 0x8034: pixel = 2; break;
            case 0x8DAD: pixel = 8; break;
            default: pixel = 4;
        }

        return Math.max(0, width) * Math.max(0, height) * Math.max(1, depth) * pixel;
    },

    // An image source of WebGL 1 (an image, a canvas, a video, a bitmap) has its own size.
    $telemetryGlCallsSourceWidth: function (source) {
        return source ? (source.videoWidth || source.naturalWidth || source.width || 0) : 0;
    },

    $telemetryGlCallsSourceHeight: function (source) {
        return source ? (source.videoHeight || source.naturalHeight || source.height || 0) : 0;
    },

    // Wraps the methods of a prototype of the contexts, once, when framework.js starts: Unity compiles most of its shaders while it
    // loads, before any managed code. A wrapper calls the original with the same this and arguments and returns what it returns, an
    // exception of the original goes on as it was; only the calls of the context of Unity (GLctx) are counted, another canvas of the
    // page is not. A lost context keeps its object, a new one has the same prototype.
    $telemetryGlCallsWrap__deps: ['$telemetryGlCallsState', '$telemetryGlCallsTimed', '$telemetryGlCallsDataBytes',
        '$telemetryGlCallsImageBytes', '$telemetryGlCallsSourceWidth', '$telemetryGlCallsSourceHeight'],
    $telemetryGlCallsWrap: function (prototype) {
        if (!prototype || Object.prototype.hasOwnProperty.call(prototype, 'telemetryGlCallsWrapped'))
            return;

        var state = telemetryGlCallsState;

        Object.defineProperty(prototype, 'telemetryGlCallsWrapped', { value: true });

        function isUnity(context) {
            return typeof GLctx !== 'undefined' && context === GLctx;
        }

        function wrap(name, count) {
            var original = prototype[name];

            if (typeof original !== 'function')
                return;

            prototype[name] = function () {
                var result = original.apply(this, arguments);

                if (isUnity(this)) {
                    try {
                        count(arguments);
                    } catch (error) {
                    }
                }

                return result;
            };
        }

        function timed(name, kind) {
            var original = prototype[name];

            if (typeof original !== 'function')
                return;

            prototype[name] = function () {
                if (!isUnity(this))
                    return original.apply(this, arguments);

                var start = performance.now();

                try {
                    return original.apply(this, arguments);
                } finally {
                    telemetryGlCallsTimed(kind, performance.now() - start);
                }
            };
        }

        timed('compileShader', 0);
        timed('getShaderParameter', 1);
        timed('getShaderInfoLog', 1);
        timed('linkProgram', 2);
        timed('getProgramParameter', 3);
        timed('getProgramInfoLog', 3);

        wrap('compileShader', function () { state.compiles++; });
        wrap('linkProgram', function () { state.links++; });

        // (target, size | data, usage, srcOffset, length). A size alone allocates without data.
        wrap('bufferData', function (args) {
            state.bufferDataCalls++;

            if (typeof args[1] !== 'number')
                state.bufferBytes += telemetryGlCallsDataBytes(args[1], args[3], args[4]);
        });

        // (target, dstOffset, data, srcOffset, length).
        wrap('bufferSubData', function (args) {
            state.bufferBytes += telemetryGlCallsDataBytes(args[2], args[3], args[4]);
        });

        // (target, level, internalformat, format, type, source) of WebGL 1, or (target, level, internalformat, width, height,
        // border, format, type, pixels | source | offset, srcOffset). Null pixels allocate without data.
        wrap('texImage2D', function (args) {
            if (args.length === 6) {
                state.textureUploads++;
                state.textureBytes += telemetryGlCallsImageBytes(telemetryGlCallsSourceWidth(args[5]), telemetryGlCallsSourceHeight(args[5]), 1, args[3], args[4]);
                return;
            }

            if (args[8] === null || args[8] === undefined)
                return;

            state.textureUploads++;
            state.textureBytes += telemetryGlCallsImageBytes(args[3], args[4], 1, args[6], args[7]);
        });

        // (target, level, x, y, format, type, source) of WebGL 1, or (target, level, x, y, width, height, format, type, pixels, ...).
        wrap('texSubImage2D', function (args) {
            state.textureUploads++;

            if (args.length === 7)
                state.textureBytes += telemetryGlCallsImageBytes(telemetryGlCallsSourceWidth(args[6]), telemetryGlCallsSourceHeight(args[6]), 1, args[4], args[5]);
            else
                state.textureBytes += telemetryGlCallsImageBytes(args[4], args[5], 1, args[6], args[7]);
        });

        // (target, level, internalformat, width, height, depth, border, format, type, pixels, ...).
        wrap('texImage3D', function (args) {
            if (args[9] === null || args[9] === undefined)
                return;

            state.textureUploads++;
            state.textureBytes += telemetryGlCallsImageBytes(args[3], args[4], args[5], args[7], args[8]);
        });

        // (target, level, x, y, z, width, height, depth, format, type, pixels, ...).
        wrap('texSubImage3D', function (args) {
            state.textureUploads++;
            state.textureBytes += telemetryGlCallsImageBytes(args[5], args[6], args[7], args[8], args[9]);
        });

        // The data at its index: a view with srcOffset and srcLengthOverride after it, or the size of the data in a pixel buffer.
        function compressed(name, index) {
            wrap(name, function (args) {
                state.textureUploads++;
                state.textureBytes += telemetryGlCallsDataBytes(args[index], args[index + 1], args[index + 2]);
            });
        }

        compressed('compressedTexImage2D', 6);
        compressed('compressedTexSubImage2D', 7);
        compressed('compressedTexImage3D', 7);
        compressed('compressedTexSubImage3D', 9);
    },

    $telemetryGlCallsWrapAll__deps: ['$telemetryGlCallsWrap'],
    $telemetryGlCallsWrapAll: function () {
        try {
            if (typeof WebGL2RenderingContext !== 'undefined')
                telemetryGlCallsWrap(WebGL2RenderingContext.prototype);

            if (typeof WebGLRenderingContext !== 'undefined')
                telemetryGlCallsWrap(WebGLRenderingContext.prototype);
        } catch (error) {
        }
    },

    // The threshold of a slow call in milliseconds, from the build of the scope; the wrappers are in place since the start.
    TelemetryGlCallsInstall__deps: ['$telemetryGlCallsState', '$telemetryGlCallsWrapAll'],
    TelemetryGlCallsInstall__postset: 'telemetryGlCallsWrapAll();',
    TelemetryGlCallsInstall: function (slowMs) {
        telemetryGlCallsState.slowMs = slowMs;
    },

    // Writes the counters and the slow calls into the doubles at the pointer (TelemetryGlCallsLayout) and starts them again. The
    // number of the slow calls written.
    TelemetryGlCallsTake__deps: ['$telemetryGlCallsState'],
    TelemetryGlCallsTake: function (into, maxSlow) {
        var state = telemetryGlCallsState;
        var at = into >> 3;

        HEAPF64[at] = state.compiles;
        HEAPF64[at + 1] = state.compileMs;
        HEAPF64[at + 2] = state.links;
        HEAPF64[at + 3] = state.linkMs;
        HEAPF64[at + 4] = state.bufferBytes;
        HEAPF64[at + 5] = state.bufferDataCalls;
        HEAPF64[at + 6] = state.textureBytes;
        HEAPF64[at + 7] = state.textureUploads;

        var slow = Math.min(state.slowKinds.length, maxSlow);

        for (var i = 0; i < slow; i++) {
            HEAPF64[at + 8 + i * 2] = state.slowKinds[i];
            HEAPF64[at + 9 + i * 2] = state.slowDurations[i];
        }

        state.compiles = 0;
        state.compileMs = 0;
        state.links = 0;
        state.linkMs = 0;
        state.bufferBytes = 0;
        state.bufferDataCalls = 0;
        state.textureBytes = 0;
        state.textureUploads = 0;
        state.slowKinds.length = 0;
        state.slowDurations.length = 0;

        return slow;
    }
};

mergeInto(LibraryManager.library, LibraryTelemetryGlCalls);
