var LibraryTelemetryGlCallErrors = {
    // The errors of the calls of WebGL with the name of the call. Two sources:
    // - the errors emscripten records itself (GL.recordError: an enum or a value its own code of a gl* function rejects, the
    //   context is never asked), always, from the start of framework.js; only glGetError of the build would see them otherwise;
    // - the check of every call of the context of Unity (GLctx) with getError after it, only on a page opened with ?glcheck (or
    //   ?glcheck=1): the call, its arguments, the frame and the time of the first error of each code and call. Each getError
    //   is a round trip to the GPU process: the check is a slow diagnostic mode, a page without the parameter wraps nothing.
    // The codes the check takes from the context are the ones TelemetryGlErrorsTake would have found: it gets them from here.
    // Numbers and arrays only in the $ entries: emscripten puts their values into framework.js as code.
    $telemetryGlCallErrorsState: {
        maxDetails: 32,
        maxArgumentsLength: 96,
        isChecking: 0,
        frame: 0,
        recorded: 0,
        checked: 0,
        codes: [],
        seen: [],
        details: []
    },

    // The first gl* function of the stack under the wrapper, as emscripten names it (_glBindTexture, _emscripten_glGetIntegerv,
    // emscriptenWebGLGet under _glGetIntegerv): the name without the prefix. A stack of V8 (" at name (") or of
    // SpiderMonkey and JavaScriptCore ("name@").
    $telemetryGlCallErrorsCaller: function () {
        var lines = String(new Error().stack || '').split('\n');
        var first = '';

        for (var i = 0; i < lines.length; i++) {
            var match = /^\s*at ([^\s(]+)/.exec(lines[i]) || /^([^@\s]*)@/.exec(lines[i]);
            var name = match ? (/[\w$]+$/.exec(match[1]) || [''])[0] : '';

            if (!name || name === 'telemetryGlCallErrorsCaller' || name === 'recordError')
                continue;

            var gl = /^_(?:emscripten_)?(gl[A-Z]\w*)$/.exec(name);

            if (gl)
                return gl[1];

            if (!first)
                first = name;
        }

        return first || 'unknown';
    },

    // The arguments of a call, short: numbers (enums from 0x100 in hex), null, booleans, strings cut, an array by its type and length,
    // an object of WebGL by its type.
    $telemetryGlCallErrorsArguments__deps: ['$telemetryGlCallErrorsState'],
    $telemetryGlCallErrorsArguments: function (args) {
        var parts = [];

        for (var i = 0; i < args.length; i++) {
            var value = args[i];
            var text;

            if (typeof value === 'number')
                text = value >= 0x100 && value === Math.floor(value) ? '0x' + value.toString(16) : String(value);
            else if (value === null || value === undefined || typeof value === 'boolean')
                text = String(value);
            else if (typeof value === 'string')
                text = JSON.stringify(value.length > 16 ? value.substring(0, 16) + '...' : value);
            else if (typeof value.length === 'number' && value.constructor)
                text = value.constructor.name + '(' + value.length + ')';
            else
                text = value.constructor ? value.constructor.name : typeof value;

            parts.push(text);
        }

        var joined = parts.join(',');
        var max = telemetryGlCallErrorsState.maxArgumentsLength;

        return joined.length > max ? joined.substring(0, max) + '...' : joined;
    },

    // One line of the take, tab separated (TelemetryGlCallErrors.cs): the source, the code, the call, the arguments, the frame of the
    // check (-1 without it), the time since the origin of the page. The first one of each source, code and call of the page.
    $telemetryGlCallErrorsDetail__deps: ['$telemetryGlCallErrorsState'],
    $telemetryGlCallErrorsDetail: function (source, code, call, args) {
        var state = telemetryGlCallErrorsState;
        var key = source + ' ' + code + ' ' + call;

        if (state.seen.indexOf(key) >= 0 || state.seen.length >= state.maxDetails)
            return;

        state.seen.push(key);
        state.details.push([source, code, call, args, state.isChecking ? state.frame : -1, performance.now().toFixed(1)].join('\t'));
    },

    // The recorder of emscripten keeps the first code until glGetError of the build; it is left as it is, only seen.
    $telemetryGlCallErrorsRecord__deps: ['$GL', '$telemetryGlCallErrorsState', '$telemetryGlCallErrorsCaller', '$telemetryGlCallErrorsDetail'],
    $telemetryGlCallErrorsRecord: function () {
        var original = GL.recordError;

        GL.recordError = function (code) {
            try {
                telemetryGlCallErrorsState.recorded++;
                telemetryGlCallErrorsDetail('emscripten', code, telemetryGlCallErrorsCaller(), '');
            } catch (error) {
            }

            return original.apply(this, arguments);
        };
    },

    // Every method of a prototype of the contexts but getError: the original with the same this and arguments, then, for the context
    // of Unity only, getError until NO_ERROR. A lost context gives CONTEXT_LOST_WEBGL once, as to the take.
    $telemetryGlCallErrorsCheck__deps: ['$telemetryGlCallErrorsState', '$telemetryGlCallErrorsArguments', '$telemetryGlCallErrorsDetail'],
    $telemetryGlCallErrorsCheck: function (prototype) {
        if (!prototype || Object.prototype.hasOwnProperty.call(prototype, 'telemetryGlCallErrorsChecked'))
            return;

        Object.defineProperty(prototype, 'telemetryGlCallErrorsChecked', { value: true });

        var state = telemetryGlCallErrorsState;
        var getError = prototype.getError;

        function caught(code, name, args) {
            state.checked++;

            if (state.codes.indexOf(code) < 0)
                state.codes.push(code);

            telemetryGlCallErrorsDetail('check', code, name, telemetryGlCallErrorsArguments(args));
        }

        function wrap(name) {
            var original = prototype[name];

            prototype[name] = function () {
                var result = original.apply(this, arguments);

                if (typeof GLctx !== 'undefined' && this === GLctx) {
                    try {
                        for (var i = 0; i < 4; i++) {
                            var code = getError.call(this);

                            if (code === 0)
                                break;

                            caught(code, name, arguments);
                        }
                    } catch (error) {
                    }
                }

                return result;
            };
        }

        var names = Object.getOwnPropertyNames(prototype);

        for (var i = 0; i < names.length; i++) {
            var descriptor = Object.getOwnPropertyDescriptor(prototype, names[i]);

            if (names[i] !== 'getError' && names[i] !== 'constructor' && descriptor && typeof descriptor.value === 'function')
                wrap(names[i]);
        }
    },

    $telemetryGlCallErrorsIsCheckOn: function () {
        try {
            var value = new URLSearchParams(location.search).get('glcheck');
            return value !== null && value !== '0' && value !== 'false';
        } catch (error) {
            return false;
        }
    },

    $telemetryGlCallErrorsStart__deps: ['$telemetryGlCallErrorsState', '$telemetryGlCallErrorsRecord', '$telemetryGlCallErrorsCheck',
        '$telemetryGlCallErrorsIsCheckOn'],
    $telemetryGlCallErrorsStart: function () {
        try {
            telemetryGlCallErrorsRecord();

            if (!telemetryGlCallErrorsIsCheckOn())
                return;

            var state = telemetryGlCallErrorsState;
            state.isChecking = 1;

            if (typeof WebGL2RenderingContext !== 'undefined')
                telemetryGlCallErrorsCheck(WebGL2RenderingContext.prototype);

            if (typeof WebGLRenderingContext !== 'undefined')
                telemetryGlCallErrorsCheck(WebGLRenderingContext.prototype);

            function tick() {
                state.frame++;
                requestAnimationFrame(tick);
            }

            requestAnimationFrame(tick);
        } catch (error) {
        }
    },

    // Writes [recorded, checked, isChecking] into the ints at the pointer and starts the counters again. The number of the lines waiting.
    TelemetryGlCallErrorsTake__deps: ['$telemetryGlCallErrorsState', '$telemetryGlCallErrorsStart'],
    TelemetryGlCallErrorsTake__postset: 'telemetryGlCallErrorsStart();',
    TelemetryGlCallErrorsTake: function (into) {
        var state = telemetryGlCallErrorsState;
        var at = into >> 2;

        HEAP32[at] = state.recorded;
        HEAP32[at + 1] = state.checked;
        HEAP32[at + 2] = state.isChecking;

        state.recorded = 0;
        state.checked = 0;

        return state.details.length;
    },

    // The lines waiting, one per line, or null when none.
    TelemetryGlCallErrorsReadDetails__deps: ['$telemetryGlCallErrorsState'],
    TelemetryGlCallErrorsReadDetails: function () {
        var state = telemetryGlCallErrorsState;

        if (state.details.length === 0)
            return 0;

        var text = state.details.join('\n');
        state.details.length = 0;

        var size = lengthBytesUTF8(text) + 1;
        var pointer = _malloc(size);
        stringToUTF8(text, pointer, size);

        return pointer;
    }
};

mergeInto(LibraryManager.library, LibraryTelemetryGlCallErrors);
