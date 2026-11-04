// Remote log of a page for the LAN bench: sends the console, the errors, the page events and a frame window every
// 2 s to POST /log of lan/serve.py (same origin), one line per entry: "<ms since the script> <kind> <text>".
//
// Kinds: start (user agent, dpr, screen, query), console.log/info/warn/error, error, rejection, alert (window.alert
// is replaced: a modal stops the frame loop of a phone), visibility, pagehide, and
//   frames n=<frames> avg=<ms> max=<ms> fps=<n> long=<frames over 50 ms> gap=<ms since the last window>
// measured by an own requestAnimationFrame chain; gap well above 2000 means the page was frozen or hidden.
//
// Put into a copy of a build with lan/inject.sh <build-dir> (copies this file next to index.html and loads it first
// in <head>); the template of the game is not changed. Telemetry of the game does not reach the production meta from a
// LAN origin (403), so this log is the measurement of a local build; lan/windows.py cuts it into windows.
(function () {
    var t0 = performance.now();
    function send(kind, text) {
        try {
            var x = new XMLHttpRequest();
            x.open('POST', '/log', true);
            x.send(Math.round(performance.now() - t0) + 'ms ' + kind + ' ' + String(text).slice(0, 2000));
        } catch (e) {
            // The log must never break the page it observes.
        }
    }

    send('start', navigator.userAgent + ' dpr=' + window.devicePixelRatio + ' screen=' + screen.width + 'x' + screen.height +
        ' inner=' + innerWidth + 'x' + innerHeight + ' url=' + location.search);
    window.addEventListener('error', function (e) {
        send('error', e.message + ' @' + e.filename + ':' + e.lineno + ' ' + (e.error && e.error.stack || ''));
    });
    window.addEventListener('unhandledrejection', function (e) {
        send('rejection', e.reason && (e.reason.stack || e.reason));
    });
    ['log', 'info', 'warn', 'error'].forEach(function (level) {
        var original = console[level];
        console[level] = function () {
            send('console.' + level, Array.prototype.slice.call(arguments).join(' '));
            return original.apply(console, arguments);
        };
    });
    document.addEventListener('visibilitychange', function () { send('visibility', document.visibilityState); });
    window.addEventListener('pagehide', function () { send('pagehide', ''); });
    window.alert = function (m) { send('alert', m); };

    var frames = 0, sum = 0, max = 0, long = 0, last = 0, windowStart = performance.now();
    function frame(now) {
        if (last) {
            var dt = now - last;
            frames++; sum += dt; if (dt > max) max = dt; if (dt > 50) long++;
        }
        last = now;
        requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
    setInterval(function () {
        var now = performance.now(), span = now - windowStart;
        send('frames', 'n=' + frames + ' avg=' + (frames ? (sum / frames).toFixed(1) : '-') + ' max=' + Math.round(max) +
            ' fps=' + Math.round(frames * 1000 / span) + ' long=' + long + ' gap=' + Math.round(span));
        frames = 0; sum = 0; max = 0; long = 0; windowStart = now;
    }, 2000);
})();
