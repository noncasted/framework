mergeInto(LibraryManager.library,
    {
        // The mp3 bytes of a catalog clip become a blob URL that UnityWebRequestMultimedia downloads and the
        // browser decodes (see EncodedAudio.cs). The returned string is freed by the marshaller.
        EncodedAudioCreateUrl: function (bytesPtr, length) {
            const bytes = HEAPU8.slice(bytesPtr, bytesPtr + length);
            const url = URL.createObjectURL(new Blob([bytes], { type: 'audio/mpeg' }));
            const size = lengthBytesUTF8(url) + 1;
            const buffer = _malloc(size);
            stringToUTF8(url, buffer, size);
            return buffer;
        },

        EncodedAudioRevokeUrl: function (urlPtr) {
            URL.revokeObjectURL(UTF8ToString(urlPtr));
        }
    });
