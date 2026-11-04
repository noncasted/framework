// What run.mjs lets the page reach. The host resolver rules of the stand map the page host to the local server, the meta
// host to 127.0.0.1:1 (refused) and every other host to ~NOTFOUND, so nothing of the page leaves the machine. This guard
// checks that and records it in summary.meta:
//   sockets   every websocket of the page: {at, url, outcome}
//   attempts  every request or websocket to the meta host; refused counts the ones that ended refused
//   requests  fetch / XHR to the page host that are not files of the served build (an API of the game on the page host)
//   blocked   requests to any other host (never resolved; a third-party dependency of the build shows here)
//   warnings  a websocket to the page host (the build takes its meta url from the page: an empty or relative url, so
//             metaHost of the config is not what the build uses), a non-file request to the page host, a blocked host
//   leaks     an answer from the meta host or another host: onLeak is called at once (exit 3)
// The page host is served locally, so a websocket to it is a warning, not a leak.
const sinceStart = (t0) => +((Date.now() - t0) / 1000).toFixed(1);
const REFUSED = 'net::ERR_CONNECTION_REFUSED';
const NOT_RESOLVED = 'net::ERR_NAME_NOT_RESOLVED';
const NETWORK_SCHEMES = new Set(['http:', 'https:', 'ws:', 'wss:']);

// The rules of --host-resolver-rules: the page host to the local server, the meta host cut (or, for --leak-test, to the
// local server too), everything else unresolved. The first matching rule wins.
export function hostRules(host, metaHost, port, leakTest) {
    const meta = leakTest ? `MAP ${metaHost}:443 127.0.0.1:${port}` : `MAP ${metaHost} 127.0.0.1:1`;
    return `MAP ${host}:443 127.0.0.1:${port}, ${meta}, MAP * ~NOTFOUND`;
}

// isBuildFile(url) tells a file of the served build (null when the stand does not serve the build itself).
export function guardNetwork(session, { host, metaHost, isBuildFile }, t0, onLeak, onWarning) {
    const state = { sockets: [], attempts: [], refused: 0, requests: [], blocked: [], warnings: [], leaks: [] };
    const tracked = new Map();
    const kindOf = (url) => {
        if (!URL.canParse(url)) return null;
        const u = new URL(url);
        if (!NETWORK_SCHEMES.has(u.protocol)) return null;
        return u.hostname === host ? 'page' : u.hostname === metaHost ? 'meta' : 'other';
    };
    const warn = (text) => {
        const entry = `${sinceStart(t0)}s ${text}`;
        state.warnings.push(entry);
        onWarning(entry);
    };
    const leak = (what, url) => {
        const entry = `${sinceStart(t0)}s ${what} ${url}`;
        state.leaks.push(entry);
        onLeak(entry);
    };
    const begin = (requestId, kind, url, what) => {
        if (kind === 'meta')
            state.attempts.push(`${sinceStart(t0)}s ${what} ${url}`);
        if (kind === 'other') {
            state.blocked.push(`${sinceStart(t0)}s ${what} ${url}`);
            warn(`${what} to ${new URL(url).hostname}, a host the stand does not serve (blocked): ${url}`);
        }
        if (kind !== 'page')
            tracked.set(requestId, { kind, url });
    };
    // A request to the meta host may only end refused, one to another host only unresolved.
    const failed = (t, errorText, why) => {
        if (t.kind === 'meta' && errorText.includes(REFUSED)) state.refused++;
        else if (!(t.kind === 'other' && errorText.includes(NOT_RESOLVED)))
            leak(`failed with ${errorText}${why} (something answered)`, t.url);
    };

    session.on('Network.requestWillBeSent', (p) => {
        const kind = kindOf(p.request.url);
        if (!kind) return;
        if (kind === 'page' && isBuildFile && (p.type === 'Fetch' || p.type === 'XHR') && !isBuildFile(p.request.url)) {
            state.requests.push(`${sinceStart(t0)}s ${p.request.method} ${p.request.url}`);
            warn(`${p.type} to the page host, not a file of the build: ${p.request.method} ${p.request.url}`);
        }
        begin(p.requestId, kind, p.request.url, p.request.method);
    });
    session.on('Network.webSocketCreated', (p) => {
        const kind = kindOf(p.url);
        if (!kind) return;
        const socket = { at: sinceStart(t0), url: p.url, outcome: 'created' };
        state.sockets.push(socket);
        if (kind === 'page')
            warn(`WEBSOCKET TO THE PAGE HOST ${p.url}: the build does not use metaHost ${metaHost} (an empty or relative meta ` +
                'url?); the local server got it, so nothing left the machine, but the meta check saw nothing');
        begin(p.requestId, kind, p.url, 'websocket');
        tracked.set(p.requestId, { kind, url: p.url, socket });
    });
    session.on('Network.responseReceived', (p) => {
        const t = tracked.get(p.requestId);
        if (t && t.kind !== 'page') leak(`response ${p.response.status}`, t.url);
    });
    session.on('Network.loadingFinished', (p) => {
        const t = tracked.get(p.requestId);
        if (t && t.kind !== 'page') leak('loading finished', t.url);
    });
    session.on('Network.loadingFailed', (p) => {
        const t = tracked.get(p.requestId);
        if (t && t.kind !== 'page' && !t.socket) failed(t, p.errorText, p.corsErrorStatus ? ' ' + p.corsErrorStatus.corsError : '');
    });
    const socketEvent = (event, outcome) => session.on(`Network.${event}`, (p) => {
        const t = tracked.get(p.requestId);
        if (!t?.socket) return;
        t.socket.outcome = outcome(p, t.socket.outcome);
        if (t.kind !== 'page') leak(event, t.url);
    });
    socketEvent('webSocketHandshakeResponseReceived', (p) => `handshake ${p.response.status}`);
    socketEvent('webSocketFrameReceived', () => 'open');
    socketEvent('webSocketFrameSent', () => 'open');
    session.on('Network.webSocketClosed', (p) => {
        const t = tracked.get(p.requestId);
        if (t?.socket && !t.socket.outcome.startsWith('failed')) t.socket.outcome += ', closed';
    });
    // A failed websocket shows its reason only in the log: "WebSocket connection to '<url>' failed: <reason>".
    session.on('Log.entryAdded', (p) => {
        const m = /^WebSocket connection to '([^']+)' failed: (.*)$/.exec(p.entry.text);
        if (!m) return;
        const socket = state.sockets.findLast(s => s.url === m[1] && !s.outcome.startsWith('failed'));
        if (socket) socket.outcome = `failed: ${m[2]}`;
        const kind = kindOf(m[1]);
        if (kind && kind !== 'page') failed({ kind, url: m[1] }, m[2], '');
    });
    return state;
}
