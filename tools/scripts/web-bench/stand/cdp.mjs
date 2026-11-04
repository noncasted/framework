// Headless Chromium and a minimal CDP client of its first page (node >= 22: the built-in WebSocket and fetch).
// Used by run.mjs; not a command of its own.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

export const CHROMIUM = process.env.CHROMIUM || '/usr/bin/chromium';

// Starts Chromium with a fresh profile and connects to its page. `flags` go after the stand's own ones.
export async function launch(flags) {
    if (!fs.existsSync(CHROMIUM))
        throw new Error(`Chromium not found at ${CHROMIUM} (set CHROMIUM)`);
    const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'web-bench-chrome-'));
    const chrome = spawn(CHROMIUM, ['--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`,
        '--no-first-run', '--no-default-browser-check', '--autoplay-policy=no-user-gesture-required', ...flags, 'about:blank'],
        { stdio: ['ignore', 'ignore', 'pipe'], detached: true });
    const browserWs = await new Promise((resolve, reject) => {
        let buffer = '';
        const timer = setTimeout(() => reject(new Error('Chromium did not print its DevTools url in 20 s')), 20000);
        chrome.stderr.on('data', (d) => {
            buffer += d;
            const m = buffer.match(/DevTools listening on (ws:\/\/\S+)/);
            if (m) { clearTimeout(timer); resolve(m[1]); }
        });
        chrome.on('exit', (code) => { clearTimeout(timer); reject(new Error(`Chromium exited with ${code} before DevTools: ${buffer.slice(-600)}`)); });
    });
    const list = await (await fetch(browserWs.replace('ws://', 'http://').replace(/\/devtools\/browser.*/, '/json/list'))).json();
    const page = list.find(t => t.type === 'page');
    if (!page)
        throw new Error('Chromium has no page target');
    const session = await connect(page.webSocketDebuggerUrl);
    session.chrome = chrome;
    // Chromium leads its own process group (detached): the group is killed, so no renderer or GPU process outlives it and
    // writes into the profile while it is removed. Throws when the group is still alive after 10 s.
    session.close = async () => {
        session.closing = true;
        session.ws.close();
        killGroup(chrome.pid, 'SIGKILL');
        for (const started = Date.now(); groupAlive(chrome.pid); await new Promise(r => setTimeout(r, 100))) {
            if (Date.now() - started > 10000)
                throw new Error(`Chromium group ${chrome.pid} still alive 10 s after SIGKILL; profile ${profile} kept`);
        }
        fs.rmSync(profile, { recursive: true, force: true, maxRetries: 5 });
    };
    chrome.on('exit', (code, signal) => {
        if (!session.closing) session.fail(new Error(`Chromium exited (${code ?? signal})`));
    });
    return session;
}

function killGroup(pid, signal) {
    try {
        process.kill(-pid, signal);
    } catch (e) {
        if (e.code !== 'ESRCH') throw e;
    }
}

// True while any process of the group lives; a zombie leader counts until node reaps it.
function groupAlive(pid) {
    try {
        process.kill(-pid, 0);
        return true;
    } catch (e) {
        if (e.code === 'ESRCH') return false;
        throw e;
    }
}

// A CDP session over one websocket: send(method, params, timeoutMs), on(method, handler), fail(error).
async function connect(url) {
    const ws = new WebSocket(url);
    await new Promise((resolve, reject) => {
        ws.addEventListener('open', resolve, { once: true });
        ws.addEventListener('error', () => reject(new Error(`CDP websocket ${url} failed to open`)), { once: true });
    });
    let id = 0;
    const pending = new Map();
    const handlers = new Map();
    const session = { ws, failed: null, onFail: [] };
    session.fail = (error) => {
        if (session.failed)
            return;
        session.failed = error;
        for (const p of pending.values())
            p.reject(error);
        pending.clear();
        for (const f of session.onFail)
            f(error);
    };
    ws.addEventListener('message', (e) => {
        const m = JSON.parse(e.data);
        if (m.id) {
            const p = pending.get(m.id);
            if (!p)
                return;
            pending.delete(m.id);
            clearTimeout(p.timer);
            if (m.error)
                p.reject(new Error(`${p.method}: ${m.error.message} ${m.error.data ?? ''}`));
            else
                p.resolve(m.result);
            return;
        }
        for (const h of handlers.get(m.method) ?? [])
            h(m.params);
    });
    ws.addEventListener('close', () => session.fail(new Error('CDP websocket closed')));
    session.send = (method, params = {}, timeoutMs = 30000) => new Promise((resolve, reject) => {
        if (session.failed)
            return reject(session.failed);
        const n = ++id;
        const timer = setTimeout(() => {
            pending.delete(n);
            reject(new Error(`${method}: no answer in ${timeoutMs} ms`));
        }, timeoutMs);
        pending.set(n, { resolve, reject, timer, method });
        ws.send(JSON.stringify({ id: n, method, params }));
    });
    session.on = (method, handler) => {
        if (!handlers.has(method))
            handlers.set(method, []);
        handlers.get(method).push(handler);
    };
    // Evaluates an expression in the page (promises awaited); a thrown exception rejects with its description.
    session.evaluate = async (expression, timeoutMs = 30000) => {
        const r = await session.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true }, timeoutMs);
        if (r.exceptionDetails)
            throw new Error(`evaluate ${expression.slice(0, 120)}: ${r.exceptionDetails.exception?.description ?? r.exceptionDetails.text}`);
        return r.result.value;
    };
    return session;
}
