// WebContentFetch_KeepOnly of WebContentFetch.jslib on node: the cache of the page is a stub, the jslib is loaded as
// emscripten merges it. Run by WebContentFetchJslibTests (EditMode), or by hand: node --test Tests~/web-content-fetch.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Content', 'Plugins', 'WebContentFetch.jslib'), 'utf8');

function load(urls) {
    const library = {};
    const entries = new Set(urls);
    const cache = {
        keys: () => Promise.resolve([...entries].map(url => ({ url }))),
        delete: request => Promise.resolve(entries.delete(request.url))
    };
    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        UTF8ToString: value => value,
        WebContentFetchCache: () => Promise.resolve(cache),
        console: { log: () => { }, error: message => { throw new Error(message); } }
    });

    vm.runInContext(source, context);
    return { library, entries };
}

async function keepOnly(urls, directory, keep) {
    const { library, entries } = load(urls);
    library.WebContentFetch_KeepOnly(directory, keep.join('\n'));
    await new Promise(resolve => setImmediate(resolve));
    return [...entries].sort();
}

const game = 'https://cdn/StreamingAssets/Content/Game';
const stream = 'https://cdn/StreamingAssets/Content/Stream';

test('the entries of the builds before of the directory go, the other directory keeps its own', async () => {
    const left = await keepOnly([
        `${game}/new.index.json`,
        `${game}/new.archive?offset=0`,
        `${game}/old.archive?offset=0`,
        `https://old-cdn/v1/StreamingAssets/Content/Game/old.index.json`,
        `${stream}/s.index.json`,
        `${stream}/s.archive?offset=64`
    ], 'Content/Game', [`${game}/new.index.json`, `${game}/new.archive`]);

    assert.deepStrictEqual(left, [
        `${game}/new.archive?offset=0`,
        `${game}/new.index.json`,
        `${stream}/s.archive?offset=64`,
        `${stream}/s.index.json`
    ]);
});

test('a directory whose name starts with the name of another one is another directory', async () => {
    const left = await keepOnly([`${game}Extra/x.index.json`, `${game}/old.index.json`], 'Content/Game', [`${game}/new.index.json`]);

    assert.deepStrictEqual(left, [`${game}Extra/x.index.json`]);
});
