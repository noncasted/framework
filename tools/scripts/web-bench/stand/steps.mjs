// The timed steps of run.mjs over a CDP session: CPU profile, GL call count, GL frame record, screenshot, resize and the
// scenario steps over the JS API of the game. Each returns the record that goes into summary.json.
import fs from 'node:fs';
import path from 'node:path';

const sleep = (ms) => new Promise(r => setTimeout(r, ms));

// The steps of the options as one list sorted by time: [{at, kind, run(session)}].
export function planSteps(o) {
    const steps = [];
    for (const at of o.prof) steps.push({ at, kind: 'prof', run: (s) => profile(s, o, at) });
    for (const at of o.count) steps.push({ at, kind: 'count', run: (s) => count(s, o, at) });
    for (const at of o.rec) steps.push({ at, kind: 'rec', run: (s) => record(s, o, at) });
    for (const at of o.shot) steps.push({ at, kind: 'shot', run: (s) => shot(s, o, at) });
    for (const r of o.resize) steps.push({ at: r.at, kind: 'resize', run: (s) => resize(s, o, r) });
    for (const [i, step] of (o.scenario ?? []).entries())
        steps.push({ at: Number(step.at), kind: 'scenario', run: (s) => scenario(s, o, step, i) });
    return steps.sort((a, b) => a.at - b.at);
}

export async function setMetrics(session, metrics, width, height) {
    await session.send('Emulation.setDeviceMetricsOverride', {
        width, height, deviceScaleFactor: metrics.dpr, mobile: metrics.mobile, screenWidth: width, screenHeight: height,
        ...(metrics.mobile ? { screenOrientation: width > height ? { type: 'landscapePrimary', angle: 90 } : { type: 'portraitPrimary', angle: 0 } } : {}),
    });
}

// The frames of the window are the GL frames the probe counted between the start and the stop of the profiler.
async function profile(session, o, at) {
    await session.send('Profiler.enable');
    await session.send('Profiler.setSamplingInterval', { interval: 200 });
    const before = await session.evaluate('__gp.glFrames');
    await session.send('Profiler.start');
    await sleep(o.profLen * 1000);
    const r = await session.send('Profiler.stop', {}, 120000);
    const frames = await session.evaluate('__gp.glFrames') - before;
    if (!frames)
        throw new Error(`prof at ${at}: no GL frame in ${o.profLen} s`);
    const file = path.join(o.out, `cpu_${at}.cpuprofile`);
    fs.writeFileSync(file, JSON.stringify(r.profile));
    return { file: path.basename(file), seconds: o.profLen, frames, frameMs: +(o.profLen * 1000 / frames).toFixed(2),
        samples: r.profile.samples.length, nodes: r.profile.nodes.length };
}

// Calls by name over the GL frames (rAF callbacks with GL work) of a window of countMs.
async function count(session, o, at) {
    await session.evaluate(`__gp.count(${o.countMs})`);
    await sleep(o.countMs);
    // A busy page (a load under --cpu) runs the closing timer of the window late: wait for it up to 30 s more.
    let done = null;
    for (const started = Date.now(); !done; await sleep(200)) {
        done = await session.evaluate('__gp.done');
        if (!done && Date.now() - started > 30000)
            throw new Error(`count at ${at}: the window of ${o.countMs} ms did not close in 30 s more`);
    }
    fs.writeFileSync(path.join(o.out, `count_${at}.json`), JSON.stringify(done, null, 1));
    const total = Object.values(done.c).reduce((x, y) => x + y, 0);
    const top = Object.entries(done.c).sort((x, y) => y[1] - x[1]).slice(0, 8)
        .map(([k, v]) => [k, done.frames ? +(v / done.frames).toFixed(1) : v]);
    return { file: `count_${at}.json`, frames: done.frames, ms: Math.round(done.ms), calls: total,
        callsPerFrame: done.frames ? +(total / done.frames).toFixed(1) : null, topPerFrame: Object.fromEntries(top) };
}

// Every call of the next GL frame (recFrames frames), given recMs to happen.
async function record(session, o, at) {
    await session.evaluate(`__gp.startRec(${o.recFrames})`);
    await sleep(o.recMs);
    const recs = await session.evaluate('__gp.recs.splice(0)');
    if (recs.length < o.recFrames)
        throw new Error(`rec at ${at}: ${recs.length} of ${o.recFrames} GL frames in ${o.recMs} ms`);
    fs.writeFileSync(path.join(o.out, `rec_${at}.json`), JSON.stringify(recs, null, 1));
    return { file: `rec_${at}.json`, frames: recs.length, calls: recs.map(r => r.length) };
}

async function shot(session, o, at) {
    const r = await session.send('Page.captureScreenshot', { format: 'png' }, 60000);
    const file = `shot_${at}.png`;
    fs.writeFileSync(path.join(o.out, file), Buffer.from(r.data, 'base64'));
    return { file };
}

async function resize(session, o, r) {
    await setMetrics(session, o.metrics, r.width, r.height);
    return { size: `${r.width}x${r.height}` };
}

// {at, eval} evaluates an expression; {at, call, args} calls window.<scenarioApi>.<call>(...args);
// {at, waitFor, timeout} waits until an expression is truthy (a promise is awaited). The value is kept, a JSON text parsed.
async function scenario(session, o, step, i) {
    let expression;
    if (step.eval !== undefined)
        expression = step.eval;
    else if (step.call !== undefined)
        expression = `window[${JSON.stringify(o.game.scenarioApi)}][${JSON.stringify(step.call)}](...${JSON.stringify(step.args ?? [])})`;
    else
        return { index: i, waitFor: step.waitFor, waitedMs: await waitFor(session, step.waitFor, Number(step.timeout ?? 60)) };
    let value = await session.evaluate(expression, 60000);
    if (typeof value === 'string' && /^[[{]/.test(value)) {
        try { value = JSON.parse(value); } catch { /* a plain text that looks like JSON stays text */ }
    }
    return { index: i, ...(step.call ? { call: step.call, args: step.args ?? [] } : { eval: step.eval }), value };
}

async function waitFor(session, expression, timeoutSeconds) {
    const started = Date.now();
    while (true) {
        if (await session.evaluate(`Promise.resolve(${expression}).then(v => !!v)`, 60000))
            return Date.now() - started;
        if (Date.now() - started > timeoutSeconds * 1000)
            throw new Error(`waitFor ${expression}: still false after ${timeoutSeconds} s`);
        await sleep(250);
    }
}
