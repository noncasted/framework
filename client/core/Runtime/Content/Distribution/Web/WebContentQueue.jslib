// The queue of the requests of WebContentFetch.jslib and the cache of the page they share. At most `limit` requests
// (WebContentFetch_Limit) go to the server at once: a level asks for many ranges, and too many requests at once exhaust
// the browser (net::ERR_INSUFFICIENT_RESOURCES). The others wait in the order they started; a slot is held until the
// body is read and is freed by an error or an abort too.
mergeInto(LibraryManager.library, {
    // Resolves when a fetch may go to the server; rejects with an AbortError when the signal aborts it first.
    $WebContentFetchSlot__deps: ['$WebContentFetches'],
    $WebContentFetchSlot: function (signal) {
        var fetches = WebContentFetches;

        if (fetches.limit < 1)
            throw new Error('[Content] WebContentFetch_Limit was not called');

        return new Promise(function (resolve, reject) {
            if (fetches.active < fetches.limit) {
                fetches.active++;
                resolve();
                return;
            }

            var waiter = { resolve: resolve };
            fetches.waiting.push(waiter);

            signal.addEventListener('abort', function () {
                var at = fetches.waiting.indexOf(waiter);

                if (at >= 0) {
                    fetches.waiting.splice(at, 1);
                    reject(new DOMException('The fetch was aborted in the queue', 'AbortError'));
                }
            }, { once: true });
        });
    },

    // The slot goes to the first waiter, or back to the pool.
    $WebContentFetchFree__deps: ['$WebContentFetches'],
    $WebContentFetchFree: function () {
        var fetches = WebContentFetches;
        var next = fetches.waiting.shift();

        if (next === undefined)
            fetches.active--;
        else
            next.resolve();
    },

    WebContentFetch_Limit__deps: ['$WebContentFetches'],
    WebContentFetch_Limit: function (limit) {
        if (limit < 1)
            throw new Error('[Content] The limit of the fetches is ' + limit);

        var fetches = WebContentFetches;
        fetches.limit = limit;

        // A higher limit lets the waiting fetches go now; a lower one takes effect as the running ones end.
        while (fetches.waiting.length > 0 && fetches.active < limit) {
            fetches.active++;
            fetches.waiting.shift().resolve();
        }
    },

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

    // A request that holds a slot of the queue until its body is read.
    $WebContentFetchQueued__deps: ['$WebContentFetchSlot', '$WebContentFetchFree'],
    $WebContentFetchQueued: function (url, init, check) {
        return WebContentFetchSlot(init.signal).then(function () {
            return fetch(url, init).then(function (response) {
                check(response);
                return response.arrayBuffer();
            }).finally(WebContentFetchFree);
        });
    }
});
