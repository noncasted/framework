// WebContentFetch_KeepOnly of WebContentFetch.jslib on node: the cache of the page is a stub, the jslib is loaded as
// emscripten merges it. Run by WebContentFetchJslibTests (EditMode), or by hand: node --test Tests~/web-content-fetch.test.js
const { test } = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '..', 'Runtime', 'Content', 'Distribution', 'Web', 'WebContentFetch.jslib'), 'utf8');

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

const content = 'https://cdn/StreamingAssets/Content';

test('the files of the builds before go, wherever they were fetched from; the kept ones stay', async () => {
    const left = await keepOnly([
        `${content}/Game.new.bundle`,
        `${content}/Game.old.bundle`,
        `${content}/Battle.new.bundle`,
        `https://old-cdn/v1/StreamingAssets/Content/Battle.old.bundle`
    ], 'Content', [`${content}/Game.new.bundle`, `${content}/Battle.new.bundle`]);

    assert.deepStrictEqual(left, [`${content}/Battle.new.bundle`, `${content}/Game.new.bundle`]);
});

test('an entry outside the content folder stays', async () => {
    const left = await keepOnly([`https://cdn/Build/x.data`, `https://cdn/StreamingAssets/Contents/y.bundle`, `${content}/old.bundle`], 'Content', []);

    assert.deepStrictEqual(left, [`https://cdn/Build/x.data`, `https://cdn/StreamingAssets/Contents/y.bundle`]);
});

// WebContentFetchGunzip: a .gz body sent as it is is unpacked, a body the browser decoded already goes as it is.
function gunzip() {
    const library = {};
    const context = vm.createContext({
        LibraryManager: { library },
        mergeInto: (target, added) => Object.assign(target, added),
        Response, Blob, DecompressionStream, Uint8Array, Promise
    });

    vm.runInContext(source, context);
    return library.$WebContentFetchGunzip;
}

test('a gzip body is unpacked', async () => {
    const raw = Buffer.from('UnityFS\0 a bundle '.repeat(50));
    const packed = require('node:zlib').gzipSync(raw);
    const buffer = packed.buffer.slice(packed.byteOffset, packed.byteOffset + packed.length);

    const unpacked = await gunzip()(buffer);

    assert.deepStrictEqual(Buffer.from(unpacked), raw);
});

test('a body that is no gzip is taken as it is', async () => {
    const raw = new Uint8Array(Buffer.from('UnityFS\0 decoded by the browser')).buffer;

    assert.strictEqual(await gunzip()(raw), raw);
});
