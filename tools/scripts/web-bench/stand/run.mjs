// Headless stand of a web build: Chromium over CDP with a phone emulated, the production host mapped to a local TLS server
// (serve_tls.py), the meta host cut, every other host unresolved, the GL probe (probe.js) injected, and timed steps
// writing into one folder.
//
// Usage (from the root of the game, or --project <dir>): node run.mjs --out <dir> [options]
// Game values come from <game>/.claude/device-lab.json (--config <path> overrides):
//   {"host", "metaHost", "builds": {"<name>": "<dir>"}, "autorunQuery": "?autorun=1", "scenarioApi": "stress"}
// Page:
//   --url <url>            default https://<host>/ + query
//   --autorun              the query starts with autorunQuery of the config
//   --query <text>         appended to the query ("&upgrades=0"; a leading & becomes ?)
//   --build <name|dir>     serve this build (a key of "builds" or a folder) with serve_tls.py for the run, stopped after;
//                          without it a server must already listen on --port
//   --port <n>             port of the local TLS server, default 8792 (a parallel stand takes another one)
//   --prod                 (or PROD=1) the real production host and meta: no mapping, no meta check. Feeds the production
//                          telemetry; only when asked explicitly
// Emulation:
//   --device <name>        mali-g52 (895x393 @2.75, touch, Android UA; default), phone-800 (800x360 @2), desktop (1280x720 @1)
//   --size WxH, --dpr <n>  override the preset
//   --cpu <rate>           Emulation.setCPUThrottlingRate (6 ~ a Mali-G52 phone)
//   --gpu                  the GPU of the machine (--enable-gpu, ANGLE gl) instead of SwiftShader
//   --angle <backend>      swiftshader (default) | gl | vulkan
// Steps, seconds after the navigation (lists are comma separated):
//   --prof <s,..>          CPU profile of --prof-len seconds (5) -> cpu_<s>.cpuprofile; its GL frames (frames, frameMs)
//                          go into the summary, where profile.py takes them when no --frames is given
//   --count <s,..>         GL calls by name over --count-ms (5000) -> count_<s>.json, calls per frame in the summary
//   --rec <s,..>           every GL call of the next --rec-frames frames (1), given --rec-ms (1500) -> rec_<s>.json
//   --shot <s,..>          screenshot -> shot_<s>.png
//   --resize <s:WxH,..>    new CSS size of the viewport
//   --err-ms <ms>          until this page time gl.getError after every GL call -> errs.json (slow)
//   --hook-record-error    patch framework.js: GL.recordError of emscripten -> errs.json emErrs
//   --scenario <file>      JSON array of {at, eval: "<js>"} | {at, call: "<method>", args: [..]} (window.<scenarioApi>.<method>)
//                          | {at, waitFor: "<js>", timeout: <s>}; returned values go to the summary (JSON text parsed)
//   --end <s>              the run lasts at least this long
// Run:
//   --out <dir>            required, new or empty: summary.json, console.txt, errs.json and the files of the steps
//   --quiet <load>         wait first with quiet.sh until the 1-minute load average is below <load>
//   --leak-test            self-test of the network check: maps the meta host to the local server instead of 127.0.0.1:1
//                          and the page fetches it, so the request gets an answer; the run must fail with exit code 3
// Network (not with --prod): the page host goes to the local server, the meta host to 127.0.0.1:1, every other host to
//   ~NOTFOUND. summary.meta lists every websocket (sockets: url, outcome), the meta attempts, the fetch / XHR to the page
//   host that are not files of the build and the blocked hosts; summary.warnings (also printed as "run: WARNING") holds a
//   websocket to the page host (the build does not use metaHost: its meta check saw nothing), those requests and the
//   blocked hosts. Check summary.meta.sockets of the first run of a new build.
// Exit codes: 0 done, 1 error of the run (a step failed, the page host answered >= 400, Chromium died), 2 wrong options
//   or environment, 3 the meta host or another host got a request through and answered (the run is stopped at once).
// Environment: CHROMIUM (default /usr/bin/chromium), DEVICE_LAB_HOME (default ~/.local/share/device-lab: certificates), PROD=1.
// Example: cd /projects/<game> && node tools/framework/tools/scripts/web-bench/stand/run.mjs --build web --autorun \
//   --device mali-g52 --cpu 6 --prof 40 --count 30 --shot 25,45 --end 50 --out /tmp/claude-1000/bench/a1
import { spawn, spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import { launch } from './cdp.mjs';
import { certDir, parseArgs, resolveOptions } from './config.mjs';
import { collectConsole, guardBuild, hookRecordError } from './guard.mjs';
import { guardNetwork, hostRules } from './network.mjs';
import { planSteps, setMetrics } from './steps.mjs';

const here = path.dirname(new URL(import.meta.url).pathname);
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const load = () => fs.readFileSync('/proc/loadavg', 'utf8').split(' ')[0];

class LeakError extends Error {}

const o = setup(process.argv.slice(2));

const summary = { url: o.url, prod: o.prod, device: o.metrics, cpu: o.cpu, angle: o.angle, machineGpu: o.gpu, args: process.argv.slice(2),
    loadBefore: load(), started: new Date().toISOString(), steps: [] };
let server = null;
let session = null;
let writeConsole = () => {};

try {
    const flags = [`--use-angle=${o.angle}`, '--enable-unsafe-swiftshader', ...(o.gpu ? ['--enable-gpu', '--ignore-gpu-blocklist'] : [])];
    if (!o.prod) {
        if (o.build)
            server = await startServer(o);
        await expectListening(o.port);
        flags.push(`--ignore-certificate-errors-spki-list=${spkiHash(o.game.host)}`);
        summary.hostRules = hostRules(o.game.host, o.game.metaHost, o.port, o.leakTest);
        flags.push(`--host-resolver-rules=${summary.hostRules}`);
    } else {
        console.error('run: PROD - the production host and meta, this run feeds the production telemetry');
    }
    session = await launch(flags);
    const failed = new Promise((_, reject) => session.onFail.push(reject));
    failed.catch(() => {});
    await Promise.race([failed, runPage(session)]);
} catch (e) {
    summary.error = e.message;
    process.exitCode = e instanceof LeakError ? 3 : 1;
    console.error(`run: ${e instanceof LeakError ? 'META LEAK' : 'error'}: ${e.message}`);
} finally {
    writeConsole();
    summary.loadAfter = load();
    summary.ended = new Date().toISOString();
    fs.writeFileSync(path.join(o.out, 'summary.json'), JSON.stringify(summary, null, 1));
    server?.kill('SIGTERM');
    await session?.close();
}
const meta = summary.meta && { sockets: summary.meta.sockets, attempts: summary.meta.attempts.length, refused: summary.meta.refused,
    requests: summary.meta.requests.length, blocked: summary.meta.blocked.length, leaks: summary.meta.leaks };
console.log(JSON.stringify({ out: o.out, error: summary.error, warnings: summary.warnings, meta,
    gpu: summary.gpu, console: summary.console, steps: summary.steps.map(s => ({ at: s.at, kind: s.kind, ...brief(s) })) }, null, 1));
process.exit();

async function runPage(s) {
    await s.send('Runtime.enable');
    await s.send('Page.enable');
    await s.send('Log.enable');
    await s.send('Network.enable');
    const t0 = Date.now();
    const consoleState = collectConsole(s, t0);
    writeConsole = () => {
        summary.console = { lines: consoleState.lines.length, errors: consoleState.errors, exceptions: consoleState.exceptions };
        fs.writeFileSync(path.join(o.out, 'console.txt'), consoleState.lines.join('\n') + '\n');
    };
    guardBuild(s, new URL(o.url).hostname, (entry) => s.fail(new Error(`the build answered ${entry}`)));
    if (!o.prod) {
        summary.meta = guardNetwork(s, { host: o.game.host, metaHost: o.game.metaHost, isBuildFile: o.build && isBuildFile },
            t0, (entry) => s.fail(new LeakError(`a request off the machine got an answer: ${entry}`)),
            (entry) => console.error(`run: WARNING ${entry}`));
        summary.warnings = summary.meta.warnings;
    }
    if (o.hookRecordError)
        summary.recordErrorHook = (await hookRecordError(s)).patched;
    if (o.metrics.userAgent)
        await s.send('Emulation.setUserAgentOverride', { userAgent: o.metrics.userAgent });
    await setMetrics(s, o.metrics, o.metrics.width, o.metrics.height);
    if (o.metrics.mobile)
        await s.send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 5 });
    if (o.cpu !== 1)
        await s.send('Emulation.setCPUThrottlingRate', { rate: o.cpu });
    await s.send('Page.addScriptToEvaluateOnNewDocument', {
        source: `window.__GP_CFG={errMs:${o.errMs}};` + fs.readFileSync(path.join(here, 'probe.js'), 'utf8') });
    await s.send('Page.navigate', { url: o.url });
    if (o.leakTest) {
        // The page itself asks the meta host, so the self-test does not depend on whether the build ever does.
        await sleep(1000);
        await s.evaluate(`fetch('https://${o.game.metaHost}/leak-test', { mode: 'no-cors' }).then(() => 0, () => 0)`);
    }
    for (const step of planSteps(o)) {
        const wait = t0 + step.at * 1000 - Date.now();
        if (wait > 0) await sleep(wait);
        const started = (Date.now() - t0) / 1000;
        console.error(`run: ${started.toFixed(1)}s ${step.kind}@${step.at}`);
        summary.steps.push({ at: step.at, kind: step.kind, startedAt: +started.toFixed(1), ...(await step.run(s)) });
    }
    const wait = t0 + o.end * 1000 - Date.now();
    if (wait > 0) await sleep(wait);
    summary.gpu = await s.evaluate('__gp.info()');
    const errs = await s.evaluate('({errs: __gp.errs, emErrs: __gp.emErrs})');
    fs.writeFileSync(path.join(o.out, 'errs.json'), JSON.stringify(errs, null, 1));
    summary.glErrors = { getError: errs.errs.length, emscripten: errs.emErrs.length };
}

// Options, the out folder and the quiet wait; a wrong setup ends the process with exit code 2 and one line.
function setup(argv) {
    try {
        const args = parseArgs(argv);
        if (args.help) {
            const lines = fs.readFileSync(new URL(import.meta.url), 'utf8').split('\n');
            const header = lines.slice(0, lines.findIndex(l => !l.startsWith('//')));
            console.log(header.map(l => l.replace(/^\/\/ ?/, '')).join('\n'));
            process.exit(0);
        }
        const options = resolveOptions(args);
        if (fs.existsSync(options.out) && fs.readdirSync(options.out).length)
            throw new Error(`--out ${options.out} is not empty`);
        fs.mkdirSync(options.out, { recursive: true });
        if (options.quiet !== null) {
            const r = spawnSync(path.join(here, 'quiet.sh'), ['--max', options.quiet], { stdio: 'inherit' });
            if (r.status !== 0)
                throw new Error(`quiet.sh failed with ${r.status}`);
        }
        return options;
    } catch (e) {
        console.error(`run: ${e.message}`);
        process.exit(2);
    }
}

function brief(step) {
    const { at, kind, startedAt, topPerFrame, ...rest } = step;
    return rest;
}

// Starts serve_tls.py for the build and waits for its "serving" line; the child is killed by its pid at the end.
async function startServer(options) {
    const child = spawn('python3', [path.join(here, 'serve_tls.py'), options.build, '--port', String(options.port), '--host', options.game.host],
        { stdio: ['ignore', 'pipe', 'pipe'] });
    await new Promise((resolve, reject) => {
        let output = '';
        const timer = setTimeout(() => reject(new Error(`serve_tls.py did not start in 30 s: ${output}`)), 30000);
        const read = (d) => {
            output += d;
            if (output.includes('serving ')) { clearTimeout(timer); resolve(); }
        };
        child.stdout.on('data', read);
        child.stderr.on('data', read);
        child.on('exit', (code) => { clearTimeout(timer); reject(new Error(`serve_tls.py exited with ${code}: ${output.trim()}`)); });
    });
    console.error(`run: serving ${options.build} on 127.0.0.1:${options.port} (pid ${child.pid})`);
    return child;
}

function expectListening(port) {
    return new Promise((resolve, reject) => {
        const socket = net.connect(port, '127.0.0.1', () => { socket.end(); resolve(); });
        socket.on('error', () => reject(new Error(`nothing listens on 127.0.0.1:${port}: start serve_tls.py <build> --port ${port}, or pass --build`)));
    });
}

// A file of the served build: the path of the url exists under the build folder.
function isBuildFile(url) {
    const file = path.join(o.build, decodeURIComponent(new URL(url).pathname));
    return file.startsWith(o.build + path.sep) && fs.existsSync(file) && fs.statSync(file).isFile();
}

// Base64 SHA-256 of the public key of the stand certificate: Chromium trusts only this key, not every bad certificate.
function spkiHash(host) {
    const file = path.join(certDir(host), 'cert.pem');
    if (!fs.existsSync(file))
        throw new Error(`no certificate ${file}: serve_tls.py makes it on its first start`);
    const der = new crypto.X509Certificate(fs.readFileSync(file)).publicKey.export({ type: 'spki', format: 'der' });
    return crypto.createHash('sha256').update(der).digest('base64');
}
