// What run.mjs watches on the page besides the network (network.mjs): the console log, the answers >= 400 of the build,
// and the patch of framework.js that reports GL.recordError of emscripten.
const sinceStart = (t0) => ((Date.now() - t0) / 1000).toFixed(1) + 's';

// The console of the page, exceptions and failed or >= 400 requests, as lines with the time since the navigation.
export function collectConsole(session, t0) {
    const lines = [];
    const state = { lines, exceptions: 0, errors: 0 };
    session.on('Runtime.consoleAPICalled', (p) => {
        if (p.type === 'error') state.errors++;
        lines.push(`${sinceStart(t0)} console.${p.type} ${p.args.map(a => a.value ?? a.description ?? '').join(' ').slice(0, 4000)}`);
    });
    session.on('Runtime.exceptionThrown', (p) => {
        state.exceptions++;
        lines.push(`${sinceStart(t0)} EXCEPTION ${p.exceptionDetails.exception?.description ?? p.exceptionDetails.text}`);
    });
    session.on('Log.entryAdded', (p) => lines.push(`${sinceStart(t0)} log.${p.entry.level} ${p.entry.text.slice(0, 400)} ${p.entry.url ?? ''}`));
    session.on('Network.loadingFailed', (p) => lines.push(`${sinceStart(t0)} request failed ${p.errorText} ${p.type}`));
    session.on('Network.responseReceived', (p) => {
        if (p.response.status >= 400) lines.push(`${sinceStart(t0)} http ${p.response.status} ${p.response.url}`);
    });
    session.on('Page.javascriptDialogOpening', (p) => {
        lines.push(`${sinceStart(t0)} DIALOG ${p.type}: ${p.message.slice(0, 4000)}`);
        session.send('Page.handleJavaScriptDialog', { accept: true });
    });
    return state;
}

// Rewrites *.framework.js* on its way to the page: GL.recordError also calls window.__glrec(code, stack) of probe.js.
// Throws (through session.fail) when the build has no recordError of the known shape.
const RECORD_ERROR = 'recordError:errorCode=>{';
export async function hookRecordError(session) {
    const state = { patched: [] };
    session.on('Fetch.requestPaused', async (p) => {
        try {
            const body = await session.send('Fetch.getResponseBody', { requestId: p.requestId }, 60000);
            const text = Buffer.from(body.body, body.base64Encoded ? 'base64' : 'utf8').toString('utf8');
            if (!text.includes(RECORD_ERROR))
                throw new Error(`--hook-record-error: no "${RECORD_ERROR}" in ${p.request.url} (another emscripten?)`);
            const patched = text.replace(RECORD_ERROR, RECORD_ERROR + 'window.__glrec&&window.__glrec(errorCode,new Error().stack);');
            const headers = p.responseHeaders.filter(h => !/^(content-encoding|content-length)$/i.test(h.name));
            await session.send('Fetch.fulfillRequest', { requestId: p.requestId, responseCode: p.responseStatusCode, responseHeaders: headers,
                body: Buffer.from(patched, 'utf8').toString('base64') }, 60000);
            state.patched.push(p.request.url);
        } catch (e) {
            session.fail(e);
        }
    });
    await session.send('Fetch.enable', { patterns: [{ urlPattern: '*.framework.js*', requestStage: 'Response' }] });
    return state;
}

// A response >= 400 from the game host is a broken or incomplete build (a file of index.html missing): onBroken at once.
export function guardBuild(session, host, onBroken) {
    session.on('Network.responseReceived', (p) => {
        if (p.response.status >= 400 && URL.canParse(p.response.url) && new URL(p.response.url).hostname === host)
            onBroken(`${p.response.status} ${p.response.url}`);
    });
}
