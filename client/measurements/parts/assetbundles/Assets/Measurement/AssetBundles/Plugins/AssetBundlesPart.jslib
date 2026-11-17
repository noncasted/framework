// The bytes of a bundle fetched by the page into the file system, where AssetBundle.LoadFromFileAsync reads them.
// Status: 0 running, 1 written, 2 failed.
mergeInto(LibraryManager.library, {
    $MeasurementBundles: { next: 1, entries: {} },

    MeasurementBundle_Fetch__deps: ['$MeasurementBundles', '$FS'],
    MeasurementBundle_Fetch: function (urlPointer, pathPointer) {
        var url = UTF8ToString(urlPointer);
        var path = UTF8ToString(pathPointer);
        var id = MeasurementBundles.next++;
        var entry = { status: 0 };
        MeasurementBundles.entries[id] = entry;

        fetch(url)
            .then(function (response) {
                if (!response.ok)
                    throw new Error('HTTP ' + response.status);

                return response.arrayBuffer();
            })
            .then(function (buffer) {
                FS.writeFile(path, new Uint8Array(buffer));
                entry.status = 1;
            })
            .catch(function (error) {
                console.error('[measurement] ' + url + ': ' + error);
                entry.status = 2;
            });

        return id;
    },

    MeasurementBundle_Status__deps: ['$MeasurementBundles'],
    MeasurementBundle_Status: function (id) {
        return MeasurementBundles.entries[id].status;
    }
});
