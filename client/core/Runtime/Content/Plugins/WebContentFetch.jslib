// The fetches of a content directory into the emscripten file system (FS), polled by WebContentFetch.cs: 0 running,
// 1 fetched, 2 failed, 3 taken from the cache of the page. The responses are kept in the Cache API (the cache "content"):
// only for URLs whose content never changes (the index and the archive are named by the hash of the build).
// An aborted fetch writes nothing and logs nothing.
// A whole file (WebContentFetch_Start, the index) is written with FS.writeFile and cached by its URL. A range of the archive
// (WebContentFetch_StartRange) is a Range request the server must answer with 206 and exactly the bytes asked for (no
// Content-Encoding: the blocks are LZ4); they are written at their offset into the archive, which exists at its full
// length, and every file wholly inside the range is cached apart under <url>?offset=<offset> (the Cache API ignores the
// fragment of a URL, and it refuses a 206). A range is read from the cache only when the cache holds all its files.
// A range bypasses the HTTP cache of the browser (no-store), which would serialize the requests of one URL.
// The engine refuses an archive whose times changed after it was mounted ("Archive file was modified while it was
// opened"): WebContentFetch_Pin records the times of the node of MEMFS, and every write after it puts them back. This
// leans on the inside of MEMFS (node.mtime / ctime / atime in ms), so a node without them throws.
// At most `limit` requests go to the server at once (WebContentFetch_Limit): the others wait in the order they started;
// a slot is held until the body is read and is freed by an error or an abort too. A read from the cache takes no slot.
mergeInto(LibraryManager.library, {
    $WebContentFetches: { next: 1, entries: {}, cache: null, limit: 0, active: 0, waiting: [], pins: {} },

    // Writes the bytes at an offset of a file that exists, and puts back the times recorded by WebContentFetch_Pin.
    $WebContentFetchWrite__deps: ['$WebContentFetches', '$FS'],
    $WebContentFetchWrite: function (path, bytes, at) {
        var stream = FS.open(path, 'r+');

        try {
            FS.write(stream, bytes, 0, bytes.length, at);
        } finally {
            FS.close(stream);
        }

        var pinned = WebContentFetches.pins[path];

        if (pinned !== undefined) {
            var node = FS.lookupPath(path).node;
            node.mtime = pinned.mtime;
            node.ctime = pinned.ctime;
            node.atime = pinned.atime;
        }
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

    WebContentFetch_Start__deps: ['$WebContentFetchCache', '$WebContentFetchRun', '$WebContentFetchQueued', '$FS'],
    WebContentFetch_Start: function (urlPointer, pathPointer) {
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

    WebContentFetch_StartRange__deps: ['$WebContentFetchCache', '$WebContentFetchRun', '$WebContentFetchQueued', '$WebContentFetchWrite'],
    WebContentFetch_StartRange: function (urlPointer, pathPointer, offset, length, filesPointer) {
        var url = UTF8ToString(urlPointer);
        var path = UTF8ToString(pathPointer);
        var end = offset + length;
        var files = UTF8ToString(filesPointer).split('\n').filter(function (line) { return line.length > 0; }).map(function (line) {
            var parts = line.split(':');
            return { offset: Number(parts[0]), length: Number(parts[1]) };
        });
        var separator = url.indexOf('?') < 0 ? '?' : '&';

        function key(file) {
            return url + separator + 'offset=' + file.offset;
        }

        return WebContentFetchRun(url + ' [' + offset + ', ' + end + ') into ' + path, function (controller) {
            function download(cache) {
                // no-store: the HTTP cache of Chromium lets one request at a time use the entry of a URL, so concurrent
                // ranges of the one archive would go to the server one after another. The Cache API keeps the bytes.
                var init = { signal: controller.signal, cache: 'no-store', headers: { Range: 'bytes=' + offset + '-' + (end - 1) } };

                return WebContentFetchQueued(url, init, function (response) {
                    if (response.status !== 206)
                        throw new Error('HTTP ' + response.status + ' instead of 206: the server must answer Range requests of the archive');

                    var encoding = response.headers.get('Content-Encoding');

                    if (encoding !== null && encoding !== 'identity')
                        throw new Error('Content-Encoding ' + encoding + ': the server must send the archive as it is');
                }).then(function (buffer) {
                    if (buffer.byteLength !== length)
                        throw new Error(buffer.byteLength + ' bytes instead of ' + length);

                    if (controller.signal.aborted)
                        return 1;

                    WebContentFetchWrite(path, new Uint8Array(buffer), offset);

                    if (cache === null)
                        return 1;

                    return Promise.all(files.map(function (file) {
                        var start = file.offset - offset;
                        return cache.put(key(file), new Response(buffer.slice(start, start + file.length)));
                    })).catch(function (error) {
                        if (!controller.signal.aborted)
                            console.error('[Content] Keeping ' + url + ' [' + offset + ', ' + end + ') in the cache failed: ' + error);
                    }).then(function () { return 1; });
                });
            }

            // Every file of the range from the cache, or null when one is missing or of another size.
            function cached(cache) {
                return Promise.all(files.map(function (file) {
                    return cache.match(key(file)).then(function (response) {
                        return response ? response.arrayBuffer() : null;
                    });
                })).then(function (buffers) {
                    for (var i = 0; i < buffers.length; i++) {
                        if (buffers[i] === null || buffers[i].byteLength !== files[i].length)
                            return null;
                    }

                    return buffers;
                });
            }

            return WebContentFetchCache().then(function (cache) {
                if (cache === null)
                    return download(null);

                return cached(cache).then(function (buffers) {
                    if (buffers === null)
                        return download(cache);

                    if (!controller.signal.aborted) {
                        for (var i = 0; i < buffers.length; i++)
                            WebContentFetchWrite(path, new Uint8Array(buffers[i]), files[i].offset);
                    }

                    return 3;
                });
            });
        });
    },

    WebContentFetch_Pin__deps: ['$WebContentFetches', '$FS'],
    WebContentFetch_Pin: function (pathPointer) {
        var path = UTF8ToString(pathPointer);
        var node = FS.lookupPath(path).node;

        if (typeof node.mtime !== 'number' || typeof node.ctime !== 'number' || typeof node.atime !== 'number')
            throw new Error('[Content] The node of ' + path + ' has no mtime / ctime / atime: the emscripten of this Unity keeps the times elsewhere, see WebContentFetch.jslib');

        WebContentFetches.pins[path] = { mtime: node.mtime, ctime: node.ctime, atime: node.atime };
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

    // Only the entries of one directory (their URL holds its folder, /Content/<name>/, on any host and path, so the
    // builds before at another URL go too): the other directories of the page keep theirs.
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
