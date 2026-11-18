// The fetches of the content bundles into the emscripten file system (FS), polled by WebContentFetch.cs: 0 running,
// 1 fetched, 2 failed, 3 taken from the cache of the page. The responses are kept in the Cache API (the cache "content"):
// only for URLs whose content never changes (a bundle file is named by its hash). An aborted fetch writes nothing and
// logs nothing. A file is written with FS.writeFile and cached by its URL. With gunzip a body that starts with the magic of
// gzip is unpacked by DecompressionStream first (a bundle the build shipped as .gz, which a host sends as it is: the CDN of
// Devvit sets no Content-Encoding for binary types); a body the browser decoded already (a host that sends
// Content-Encoding: gzip) has no such magic and is taken as it is. The cache keeps the unpacked bytes.
// At most `limit` requests go to the server at once (WebContentFetch_Limit): the others wait in the order they started;
// a slot is held until the body is read and is freed by an error or an abort too. A read from the cache takes no slot.
mergeInto(LibraryManager.library, {
    $WebContentFetches: { next: 1, entries: {}, cache: null, limit: 0, active: 0, waiting: [] },

    // The bytes of a gzip stream unpacked, other bytes as they are (decoded by the browser already). Needs DecompressionStream
    // (Chrome 80, Firefox 113, Safari 16.4).
    $WebContentFetchGunzip: function (buffer) {
        var bytes = new Uint8Array(buffer);

        if (bytes.length < 2 || bytes[0] !== 0x1f || bytes[1] !== 0x8b)
            return Promise.resolve(buffer);

        if (typeof DecompressionStream === 'undefined')
            return Promise.reject(new Error('no DecompressionStream in this browser to unpack a .gz file'));

        return new Response(new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip'))).arrayBuffer();
    },

    // Runs a fetch: work(controller) resolves to a status (1 or 3) once the bytes are written.
    $WebContentFetchRun__deps: ['$WebContentFetches'],
    $WebContentFetchRun: function (what, work) {
        var id = WebContentFetches.next++;
        var controller = new AbortController();
        var entry = { status: 0, controller: controller };
        WebContentFetches.entries[id] = entry;

        work(controller)
            .then(function (status) {
                if (!controller.signal.aborted)
                    entry.status = status;
            })
            .catch(function (error) {
                if (controller.signal.aborted)
                    return;

                console.error('[Content] Fetching ' + what + ' failed: ' + error);
                entry.status = 2;
            });

        return id;
    },

    WebContentFetch_Start__deps: ['$WebContentFetchCache', '$WebContentFetchRun', '$WebContentFetchQueued', '$WebContentFetchGunzip', '$FS'],
    WebContentFetch_Start: function (urlPointer, pathPointer, gunzip) {
        var url = UTF8ToString(urlPointer);
        var path = UTF8ToString(pathPointer);

        return WebContentFetchRun(url + ' into ' + path, function (controller) {
            function write(buffer, status) {
                if (!controller.signal.aborted)
                    FS.writeFile(path, new Uint8Array(buffer));

                return status;
            }

            function download(cache) {
                return WebContentFetchQueued(url, { signal: controller.signal }, function (response) {
                    if (!response.ok)
                        throw new Error('HTTP ' + response.status);
                }).then(function (buffer) {
                    return gunzip ? WebContentFetchGunzip(buffer) : buffer;
                }).then(function (buffer) {
                    var stored = cache === null ? Promise.resolve() : cache.put(url, new Response(buffer)).catch(function (error) {
                        if (!controller.signal.aborted)
                            console.error('[Content] Keeping ' + url + ' in the cache failed: ' + error);
                    });

                    return stored.then(function () { return write(buffer, 1); });
                });
            }

            return WebContentFetchCache().then(function (cache) {
                if (cache === null)
                    return download(null);

                return cache.match(url).then(function (cached) {
                    return cached ? cached.arrayBuffer().then(function (buffer) { return write(buffer, 3); }) : download(cache);
                });
            });
        });
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

    // Only the entries of the content (their URL holds the folder /Content/, on any host and path, so the builds before at
    // another URL go too): the other entries of the page stay.
    WebContentFetch_KeepOnly__deps: ['$WebContentFetchCache'],
    WebContentFetch_KeepOnly: function (directoryPointer, urlsPointer) {
        var directory = '/' + UTF8ToString(directoryPointer) + '/';
        var keep = new Set(UTF8ToString(urlsPointer).split('\n'));

        function url(request) {
            var at = request.url.indexOf('?');
            return at < 0 ? request.url : request.url.substring(0, at);
        }

        WebContentFetchCache()
            .then(function (cache) {
                if (cache === null)
                    return;

                return cache.keys().then(function (requests) {
                    var stale = requests.filter(function (request) { return url(request).indexOf(directory) >= 0 && !keep.has(url(request)); });

                    return Promise.all(stale.map(function (request) { return cache.delete(request); })).then(function () {
                        if (stale.length > 0)
                            console.log('[Content] Removed ' + stale.length + ' entries of the builds before from the cache');
                    });
                });
            })
            .catch(function (error) {
                console.error('[Content] Removing the files of the builds before from the cache failed: ' + error);
            });
    }
});
