// Runs a measured WebGL build in headless Chromium (the launcher of the web-bench stand, SwiftShader) until the
// scene logs its ready or failed line (Measurement.MeasurementRun), then prints one JSON object to stdout:
// { status: ready | failed | timeout, line, seconds, errors[], screenshot }.
// Usage: node smoke.mjs <build dir> <screenshot.png> [submodule-report.json]
// With a report path the build is one instrumented by the submodule profiling of the stripping tool: after the ready
// line the page builds its report (the "Download Report" button of the overlay, its download caught) into that file.
// The build is served on 127.0.0.1 (a secure context) with Content-Encoding for *.br / *.gz, as the hosting does.
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const { launch } = await import(path.join(here, '../../../tools/scripts/web-bench/stand/cdp.mjs'));

const READY = '[measurement] ready';
const FAILED = '[measurement] failed';
const TIMEOUT_MS = 120000;

const [buildArgument, screenshot, reportPath] = process.argv.slice(2);
if (!buildArgument || !screenshot)
    throw new Error('Usage: node smoke.mjs <build dir> <screenshot.png>');
const buildDir = path.resolve(buildArgument);

const TYPES = { '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream',
    '.json': 'application/json', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon' };

// The overlay of the profiling saves its report through a link to a blob; the click is caught and the text kept.
const CATCH_REPORT = `{
    const click = HTMLAnchorElement.prototype.click;
    HTMLAnchorElement.prototype.click = function () {
        if (this.download !== 'submodule_report.json')
            return click.call(this);
        fetch(this.href).then(response => response.text()).then(text => { window.__submoduleReport = text; });
    };
}`;

async function submoduleReport(session) {
    const clicked = await session.send('Runtime.evaluate', { returnByValue: true, expression:
        `(() => { const button = [...document.querySelectorAll('#submodule-profiling-summary button')].find(b => b.textContent === 'Download Report');
                  if (!button) return false; button.click(); return true; })()` });
    if (!clicked.result.value)
        throw new Error('no Download Report button: the build is not instrumented');
    for (let i = 0; i < 100; i++) {
        const report = await session.send('Runtime.evaluate', { expression: 'window.__submoduleReport ?? null', returnByValue: true });
        if (report.result.value)
            return report.result.value;
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    throw new Error('the page made no submodule report in 10 s');
}

const server = http.createServer((request, response) => {
    const url = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    requests.push(url);
    const file = path.join(buildDir, url === '/' ? 'index.html' : url);
    if (!file.startsWith(buildDir) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
        response.writeHead(404).end();
        return;
    }
    const headers = { 'Cache-Control': 'no-store' };
    let name = file;
    for (const [suffix, encoding] of [['.br', 'br'], ['.gz', 'gzip']]) {
        if (name.endsWith(suffix)) {
            headers['Content-Encoding'] = encoding;
            name = name.slice(0, -suffix.length);
        }
    }
    headers['Content-Type'] = TYPES[path.extname(name)] ?? 'application/octet-stream';
    response.writeHead(200, headers);
    fs.createReadStream(file).pipe(response);
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));

const started = Date.now();
const errors = [];
// The last lines of the console and the files the page asked for: where a page that never got ready stopped.
const consoleTail = [];
const requests = [];
let session;
let outcome;

try {
    session = await launch(['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--window-size=1280,720']);
    outcome = await new Promise(async (resolve, reject) => {
        const timer = setTimeout(() => resolve({ status: 'timeout' }), TIMEOUT_MS);
        session.onFail.push(reject);
        session.on('Runtime.consoleAPICalled', (event) => {
            const text = event.args.map(arg => arg.value ?? arg.description ?? '').join(' ');
            consoleTail.push(`${event.type}: ${text.slice(0, 300)}`);
            if (consoleTail.length > 30)
                consoleTail.shift();
            if (event.type === 'error')
                errors.push(text.slice(0, 500));
            if (text.includes(READY)) {
                clearTimeout(timer);
                resolve({ status: 'ready', line: text.slice(text.indexOf(READY)).trim() });
            }
            if (text.includes(FAILED)) {
                clearTimeout(timer);
                resolve({ status: 'failed', line: text.slice(text.indexOf(FAILED), 2000).trim() });
            }
        });
        session.on('Runtime.exceptionThrown', (event) => errors.push((event.exceptionDetails.exception?.description ?? event.exceptionDetails.text).slice(0, 500)));
        if (reportPath)
            await session.send('Page.addScriptToEvaluateOnNewDocument', { source: CATCH_REPORT });
        await session.send('Runtime.enable');
        await session.send('Page.enable');
        await session.send('Page.navigate', { url: `http://127.0.0.1:${server.address().port}/` });
    });
    if (reportPath && outcome.status === 'ready') {
        try {
            fs.writeFileSync(reportPath, await submoduleReport(session));
        } catch (error) {
            outcome.reportError = error.message;
        }
    }
    // The picture is for a look by hand: a renderer too busy to answer in time leaves the outcome as it is.
    try {
        const shot = await session.send('Page.captureScreenshot', { format: 'png' }, 90000);
        fs.writeFileSync(screenshot, Buffer.from(shot.data, 'base64'));
    } catch (error) {
        outcome.screenshotError = error.message;
    }
} finally {
    if (session)
        await session.close();
    server.close();
}

console.log(JSON.stringify({ ...outcome, seconds: Math.round((Date.now() - started) / 100) / 10, errors, console: consoleTail, requests, screenshot }));
