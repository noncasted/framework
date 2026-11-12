var LibraryTelemetryHttp = {
    // One POST of the telemetry. The callback gets the status (0: no response: the network, the timeout) and the text of the answer
    // or of the failure, exactly once. Nothing is repeated here.
    TelemetryHttpPost: function (id, urlPointer, bodyPointer, timeoutMs, callback) {
        var url = UTF8ToString(urlPointer);
        var body = UTF8ToString(bodyPointer);
        var controller = typeof AbortController === 'undefined' ? null : new AbortController();
        var timer = controller === null ? 0 : setTimeout(function () { controller.abort(); }, timeoutMs);

        fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: body,
            credentials: 'same-origin',
            signal: controller === null ? undefined : controller.signal
        }).then(function (response) {
            return response.text().then(function (text) {
                return { status: response.status, text: text };
            });
        }).catch(function (error) {
            var name = error && error.name ? error.name : String(error);
            return { status: 0, text: name === 'AbortError' ? 'timeout' : 'network: ' + name };
        }).then(function (result) {
            clearTimeout(timer);

            var size = lengthBytesUTF8(result.text) + 1;
            var pointer = _malloc(size);

            try {
                stringToUTF8(result.text, pointer, size);
                {{{ makeDynCall('viii', 'callback') }}}(id, result.status, pointer);
            } catch (error) {
                // The boundary of the page: a wasm that is dead at this point has nothing to say to, the page must not hear about it.
            } finally {
                _free(pointer);
            }
        });
    }
};

mergeInto(LibraryManager.library, LibraryTelemetryHttp);
