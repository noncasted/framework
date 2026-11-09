// Fetches a file of a content directory into the emscripten file system (FS), polled by WebContentFetch.cs:
// 0 running, 1 fetched, 2 failed, 3 taken from the cache of the page. The responses are kept in the Cache API (the
// cache "content", keyed by the URL): only for URLs whose content never changes (the files are named by their hash).
// An aborted fetch writes nothing and logs nothing. An encoded URL (<name>.br of a directory shipped with brotli) is sent
// by the server with Content-Encoding: br and decoded by the browser; the cache keeps the real bytes of a file whose size
// is the one of the index. A file of another size (the encoding missing, plain http, an HTML page for a missing file)
// fails the fetch with its reason and stays out of the cache.
mergeInto(LibraryManager.library, {
    $WebContentFetches: { next: 1, entries: {}, cache: null },

    // The cache of the page, or null without the Cache API (plain http, old browsers): then every opening fetches.
    $WebContentFetchCache__deps: ['$WebContentFetches'],
    $WebContentFetchCache: function () {
        if (WebContentFetches.cache !== null)
            return WebContentFetches.cache;

        if (typeof caches === 'undefined') {
            console.log('[Content] No Cache API on ' + location.protocol + ' : the content is fetched on every opening');
            WebContentFetches.cache = Promise.resolve(null);
        } else {
            WebContentFetches.cache = caches.open('content').catch(function (error) {
                console.log('[Content] The Cache API refused to open the cache (' + error + '): the content is fetched on every opening');
                return null;
            });
        }

        return WebContentFetches.cache;
    },

    WebContentFetch_Start__deps: ['$WebContentFetches', '$WebContentFetchCache', '$FS'],
    WebContentFetch_Start: function (urlPointer, pathPointer, size, encoded) {
        var url = UTF8ToString(urlPointer);
        var path = UTF8ToString(pathPointer);
        var id = WebContentFetches.next++;
        var controller = new AbortController();
        var entry = { status: 0, controller: controller };
        WebContentFetches.entries[id] = entry;

        function check(buffer, origin) {
            if (buffer.byteLength !== size) {
                throw new Error(buffer.byteLength + ' bytes instead of ' + size + ' ' + origin +
                    (encoded ? ': the server must send the file with Content-Encoding: br, and the browser decodes it only on https' : ''));
            }
        }

        // Only a file of the right size goes into the cache, as its real bytes.
        function download(cache) {
            return fetch(url, { signal: controller.signal }).then(function (response) {
                if (!response.ok)
                    throw new Error('HTTP ' + response.status);

                return response.arrayBuffer();
            }).then(function (buffer) {
                check(buffer, 'from the server');

                var stored = cache === null
                    ? Promise.resolve()
                    : cache.put(url, new Response(buffer)).catch(function (error) {
                        if (!controller.signal.aborted)
                            console.error('[Content] Keeping ' + url + ' in the cache failed: ' + error);
                    });

                return stored.then(function () {
                    return { buffer: buffer, status: 1 };
                });
            });
        }

        // A cached file of another size is removed: the next opening fetches it again.
        function read(cache, cached) {
            return cached.arrayBuffer().then(function (buffer) {
                if (buffer.byteLength !== size) {
                    return cache.delete(url).then(function () {
                        check(buffer, 'from the cache (removed from it)');
                    });
                }

                return { buffer: buffer, status: 3 };
            });
        }

        WebContentFetchCache()
            .then(function (cache) {
                if (cache === null)
                    return download(null);

                return cache.match(url).then(function (cached) {
                    return cached ? read(cache, cached) : download(cache);
                });
            })
            .then(function (result) {
                if (controller.signal.aborted)
                    return;

                FS.writeFile(path, new Uint8Array(result.buffer));
                entry.status = result.status;
            })
            .catch(function (error) {
                if (controller.signal.aborted)
                    return;

                console.error('[Content] Fetching ' + url + ' into ' + path + ' failed: ' + error);
                entry.status = 2;
            });

        return id;
    },

    WebContentFetch_Status__deps: ['$WebContentFetches'],
    WebContentFetch_Status: function (id) {
        var entry = WebContentFetches.entries[id];

        if (entry === undefined)
            throw new Error('[Content] No fetch ' + id);

        if (entry.status !== 0)
            delete WebContentFetches.entries[id];

        return entry.status;
    },

    WebContentFetch_Abort__deps: ['$WebContentFetches'],
    WebContentFetch_Abort: function (id) {
        var entry = WebContentFetches.entries[id];

        if (entry === undefined)
            throw new Error('[Content] No fetch ' + id + ' to abort');

        delete WebContentFetches.entries[id];
        entry.controller.abort();
    },

    WebContentFetch_KeepOnly__deps: ['$WebContentFetchCache'],
    WebContentFetch_KeepOnly: function (urlsPointer) {
        var keep = new Set(UTF8ToString(urlsPointer).split('\n'));

        WebContentFetchCache()
            .then(function (cache) {
                if (cache === null)
                    return;

                return cache.keys().then(function (requests) {
                    var stale = requests.filter(function (request) { return !keep.has(request.url); });

                    return Promise.all(stale.map(function (request) { return cache.delete(request); })).then(function () {
                        if (stale.length > 0)
                            console.log('[Content] Removed ' + stale.length + ' files of the builds before from the cache');
                    });
                });
            })
            .catch(function (error) {
                console.error('[Content] Removing the files of the builds before from the cache failed: ' + error);
            });
    }
});
