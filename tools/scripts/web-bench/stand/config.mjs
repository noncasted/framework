// Options of run.mjs: the command line, the config of the game (.claude/device-lab.json) and the device presets.
// Used by run.mjs; the options are described in its header.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const ANDROID_UA = 'Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Mobile Safari/537.36';

// Mobile presets emulate a phone in landscape: CSS size, DPR, touch and an Android user agent.
export const DEVICES = {
    'mali-g52': { width: 895, height: 393, dpr: 2.75, mobile: true, userAgent: ANDROID_UA },
    'phone-800': { width: 800, height: 360, dpr: 2, mobile: true, userAgent: ANDROID_UA },
    desktop: { width: 1280, height: 720, dpr: 1, mobile: false, userAgent: null },
};

const VALUE_FLAGS = ['project', 'config', 'out', 'url', 'query', 'build', 'port', 'device', 'size', 'dpr', 'cpu', 'angle',
    'end', 'prof', 'prof-len', 'count', 'count-ms', 'rec', 'rec-ms', 'rec-frames', 'shot', 'resize', 'err-ms', 'scenario', 'quiet'];
const BOOL_FLAGS = ['autorun', 'gpu', 'prod', 'hook-record-error', 'leak-test', 'help'];

export function parseArgs(argv) {
    const a = {};
    for (let i = 0; i < argv.length; i++) {
        const name = argv[i].replace(/^--/, '');
        if (!argv[i].startsWith('--'))
            throw new Error(`Unexpected argument ${argv[i]} (every option is --name [value])`);
        if (BOOL_FLAGS.includes(name)) { a[name] = true; continue; }
        if (!VALUE_FLAGS.includes(name))
            throw new Error(`Unknown option --${name} (see --help)`);
        if (i + 1 >= argv.length)
            throw new Error(`--${name} needs a value`);
        a[name] = argv[++i];
    }
    return a;
}

const seconds = (text, name) => {
    const v = Number(text);
    if (!Number.isFinite(v) || v < 0)
        throw new Error(`--${name}: ${text} is not a number of seconds`);
    return v;
};
const list = (text, name) => (text ?? '').split(',').filter(Boolean).map(s => seconds(s, name));

// Reads .claude/device-lab.json of the game: the values of the game are never in the scripts.
export function readGameConfig(a) {
    const project = path.resolve(a.project ?? process.cwd());
    const file = path.resolve(a.config ?? path.join(project, '.claude/device-lab.json'));
    if (!fs.existsSync(file))
        throw new Error(`No game config ${file}: run from the root of the game, or pass --project / --config`);
    const cfg = JSON.parse(fs.readFileSync(file, 'utf8'));
    for (const key of ['host', 'metaHost'])
        if (typeof cfg[key] !== 'string' || !cfg[key])
            throw new Error(`${file}: "${key}" is required`);
    return { project, file, ...cfg };
}

// The whole option set of a run, validated.
export function resolveOptions(a) {
    const game = readGameConfig(a);
    if (!a.out)
        throw new Error('--out <dir> is required');
    const prod = a.prod === true || process.env.PROD === '1';
    if (prod && a['leak-test'])
        throw new Error('--leak-test needs the local stand, not --prod');
    const device = DEVICES[a.device ?? 'mali-g52'];
    if (!device)
        throw new Error(`--device ${a.device}: one of ${Object.keys(DEVICES).join(', ')}`);
    const metrics = { ...device };
    if (a.size) {
        const m = /^(\d+)x(\d+)$/.exec(a.size);
        if (!m)
            throw new Error(`--size ${a.size}: WxH`);
        metrics.width = +m[1]; metrics.height = +m[2];
    }
    if (a.dpr)
        metrics.dpr = Number(a.dpr);
    if (a.autorun && typeof game.autorunQuery !== 'string')
        throw new Error(`--autorun: ${game.file} has no "autorunQuery"`);
    let query = (a.autorun ? game.autorunQuery : '') + (a.query ?? '');
    if (query.startsWith('&'))
        query = '?' + query.slice(1);
    const url = a.url ?? `https://${game.host}/${query}`;
    let build = null;
    if (a.build) {
        const named = game.builds?.[a.build];
        build = path.resolve(game.project, named ?? a.build);
        if (!fs.existsSync(path.join(build, 'index.html')))
            throw new Error(`--build ${a.build}: no index.html in ${build}`);
    }
    let scenario = null;
    if (a.scenario) {
        scenario = JSON.parse(fs.readFileSync(a.scenario, 'utf8'));
        if (!Array.isArray(scenario))
            throw new Error(`${a.scenario}: a JSON array of steps`);
        for (const s of scenario) {
            seconds(s.at, 'scenario at');
            if ([s.eval, s.call, s.waitFor].filter(x => x !== undefined).length !== 1)
                throw new Error(`${a.scenario}: every step has exactly one of eval / call / waitFor: ${JSON.stringify(s)}`);
            if (s.call && !game.scenarioApi)
                throw new Error(`${a.scenario}: "call" steps need "scenarioApi" in ${game.file}`);
        }
    }
    const resize = (a.resize ?? '').split(',').filter(Boolean).map(r => {
        const m = /^([\d.]+):(\d+)x(\d+)$/.exec(r);
        if (!m)
            throw new Error(`--resize ${r}: <s>:<W>x<H>`);
        return { at: +m[1], width: +m[2], height: +m[3] };
    });
    return {
        game, prod, url, build, scenario, metrics, resize,
        out: path.resolve(a.out),
        port: Number(a.port ?? 8792),
        cpu: a.cpu ? Number(a.cpu) : 1,
        gpu: a.gpu === true,
        angle: a.angle ?? (a.gpu ? 'gl' : 'swiftshader'),
        end: seconds(a.end ?? 0, 'end'),
        prof: list(a.prof, 'prof'), profLen: seconds(a['prof-len'] ?? 5, 'prof-len'),
        count: list(a.count, 'count'), countMs: Number(a['count-ms'] ?? 5000),
        rec: list(a.rec, 'rec'), recMs: Number(a['rec-ms'] ?? 1500), recFrames: Number(a['rec-frames'] ?? 1),
        shot: list(a.shot, 'shot'),
        errMs: Number(a['err-ms'] ?? 0),
        hookRecordError: a['hook-record-error'] === true,
        leakTest: a['leak-test'] === true,
        quiet: a.quiet ?? null,
    };
}

// The certificate of the local TLS server (serve_tls.py makes it on its first start).
export function certDir(host) {
    const base = process.env.DEVICE_LAB_HOME ?? path.join(os.homedir(), '.local/share/device-lab');
    return path.join(base, 'certs', host);
}
