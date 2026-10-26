// IWebRtcPeer of the browser (JsWebRtcPeer.cs): one RTCPeerConnection with one reliable ordered binary data channel
// per handler. Events go to the C# callbacks set once by JsWebRtcSetCallbacks; strings are passed as UTF-8 buffers
// that live for the call only.
var LibraryJsWebRtcPeer = {
    $jsWebRtcState: {
        handlers: {},
        lastId: 0,
        onDescription: null, // (id, type, sdp)
        onCandidate: null,   // (id, candidate, mid)
        onOpen: null,        // (id)
        onMessage: null,     // (id, buffer, length)
        onLost: null         // (id, reason)
    },

    $jsWebRtcCall__deps: ['$jsWebRtcState'],
    $jsWebRtcCall: {
        strings: function (callback, id, values) {
            const buffers = values.map(function (value) {
                const length = lengthBytesUTF8(value) + 1;
                const buffer = _malloc(length);
                stringToUTF8(value, buffer, length);
                return buffer;
            });

            try {
                if (buffers.length === 1) {
                    {{{ makeDynCall('vii', 'callback') }}}(id, buffers[0]);
                } else {
                    {{{ makeDynCall('viii', 'callback') }}}(id, buffers[0], buffers[1]);
                }
            } finally {
                buffers.forEach(function (buffer) { _free(buffer); });
            }
        },

        lost: function (handler, reason) {
            if (handler.isLost) {
                return;
            }

            handler.isLost = true;
            jsWebRtcCall.strings(jsWebRtcState.onLost, handler.id, [reason]);
        },

        // The local description goes before the candidates: the peer applies a candidate only after the description.
        describe: function (handler) {
            const description = handler.pc.localDescription;
            jsWebRtcCall.strings(jsWebRtcState.onDescription, handler.id, [description.type, description.sdp]);
            handler.isDescribed = true;

            handler.pendingCandidates.forEach(function (candidate) {
                jsWebRtcCall.strings(jsWebRtcState.onCandidate, handler.id, [candidate.candidate, candidate.sdpMid || ""]);
            });

            handler.pendingCandidates = [];
        },

        // setRemoteDescription and addIceCandidate are asynchronous: run in order, a candidate after its description.
        enqueue: function (handler, name, operation) {
            handler.chain = handler.chain.then(operation).catch(function (error) {
                jsWebRtcCall.lost(handler, name + " failed: " + error);
            });
        },

        setupChannel: function (handler, channel) {
            if (handler.channel) {
                jsWebRtcCall.lost(handler, "a second data channel: " + channel.label);
                return;
            }

            handler.channel = channel;
            channel.binaryType = "arraybuffer";

            channel.onopen = function () {
                {{{ makeDynCall('vi', 'jsWebRtcState.onOpen') }}}(handler.id);
            };

            channel.onmessage = function (event) {
                if (!(event.data instanceof ArrayBuffer)) {
                    jsWebRtcCall.lost(handler, "text message on the binary channel");
                    return;
                }

                const data = new Uint8Array(event.data);
                const buffer = _malloc(data.length);
                HEAPU8.set(data, buffer);

                try {
                    {{{ makeDynCall('viii', 'jsWebRtcState.onMessage') }}}(handler.id, buffer, data.length);
                } finally {
                    _free(buffer);
                }
            };

            channel.onclose = function () {
                jsWebRtcCall.lost(handler, "channel closed");
            };

            channel.onerror = function (event) {
                const error = event.error ? event.error.message : "unknown";
                jsWebRtcCall.lost(handler, "channel error: " + error);
            };
        }
    },

    JsWebRtcSetCallbacks: function (onDescription, onCandidate, onOpen, onMessage, onLost) {
        jsWebRtcState.onDescription = onDescription;
        jsWebRtcState.onCandidate = onCandidate;
        jsWebRtcState.onOpen = onOpen;
        jsWebRtcState.onMessage = onMessage;
        jsWebRtcState.onLost = onLost;
    },

    // iceServers: urls separated by new lines, empty for none.
    JsWebRtcCreate__deps: ['$jsWebRtcCall'],
    JsWebRtcCreate: function (iceServersPtr) {
        const servers = UTF8ToString(iceServersPtr).split("\n").filter(function (url) { return url.length > 0; });
        const id = jsWebRtcState.lastId++;
        const pc = new RTCPeerConnection({
            iceServers: servers.map(function (url) { return { urls: url }; })
        });

        const handler = {
            id: id,
            pc: pc,
            channel: null,
            chain: Promise.resolve(),
            isDescribed: false,
            isLost: false,
            pendingCandidates: []
        };

        pc.onicecandidate = function (event) {
            // null: the gathering is complete.
            if (!event.candidate || !event.candidate.candidate) {
                return;
            }

            if (handler.isDescribed) {
                jsWebRtcCall.strings(jsWebRtcState.onCandidate, id, [event.candidate.candidate, event.candidate.sdpMid || ""]);
            } else {
                handler.pendingCandidates.push(event.candidate);
            }
        };

        // The offering side: the channel is created, the offer goes out.
        pc.onnegotiationneeded = function () {
            jsWebRtcCall.enqueue(handler, "offer", function () {
                return pc.setLocalDescription().then(function () { jsWebRtcCall.describe(handler); });
            });
        };

        pc.ondatachannel = function (event) {
            jsWebRtcCall.setupChannel(handler, event.channel);
        };

        pc.onconnectionstatechange = function () {
            const state = pc.connectionState;

            if (state === "failed" || state === "disconnected" || state === "closed") {
                jsWebRtcCall.lost(handler, "connection " + state);
            }
        };

        jsWebRtcState.handlers[id] = handler;
        return id;
    },

    JsWebRtcCreateChannel__deps: ['$jsWebRtcCall'],
    JsWebRtcCreateChannel: function (id, labelPtr) {
        const handler = jsWebRtcState.handlers[id];
        jsWebRtcCall.setupChannel(handler, handler.pc.createDataChannel(UTF8ToString(labelPtr), { ordered: true }));
    },

    JsWebRtcSetRemoteDescription__deps: ['$jsWebRtcCall'],
    JsWebRtcSetRemoteDescription: function (id, typePtr, sdpPtr) {
        const handler = jsWebRtcState.handlers[id];
        const description = { type: UTF8ToString(typePtr), sdp: UTF8ToString(sdpPtr) };

        jsWebRtcCall.enqueue(handler, "remote description", function () {
            return handler.pc.setRemoteDescription(description).then(function () {
                if (description.type !== "offer") {
                    return;
                }

                return handler.pc.setLocalDescription().then(function () { jsWebRtcCall.describe(handler); });
            });
        });
    },

    JsWebRtcAddRemoteCandidate__deps: ['$jsWebRtcCall'],
    JsWebRtcAddRemoteCandidate: function (id, candidatePtr, midPtr) {
        const handler = jsWebRtcState.handlers[id];
        const candidate = { candidate: UTF8ToString(candidatePtr), sdpMid: UTF8ToString(midPtr) };

        jsWebRtcCall.enqueue(handler, "remote candidate", function () {
            return handler.pc.addIceCandidate(candidate);
        });
    },

    // 0: sent; -1: the channel is not open.
    JsWebRtcSend: function (id, bufferPtr, length) {
        const handler = jsWebRtcState.handlers[id];

        if (!handler.channel || handler.channel.readyState !== "open") {
            return -1;
        }

        handler.channel.send(HEAPU8.slice(bufferPtr, bufferPtr + length));
        return 0;
    },

    // The negotiated limit of the SCTP transport; a browser without RTCSctpTransport (old Safari) gets the size every
    // browser accepts.
    JsWebRtcMaxMessageSize: function (id) {
        const handler = jsWebRtcState.handlers[id];
        const sctp = handler.pc.sctp;
        return sctp ? Math.min(sctp.maxMessageSize, 2147483647) : 65536;
    },

    // Events after the free must not reach C#: the handlers are detached before the close.
    JsWebRtcFree: function (id) {
        const handler = jsWebRtcState.handlers[id];
        delete jsWebRtcState.handlers[id];
        handler.isLost = true;

        if (handler.channel) {
            handler.channel.onopen = null;
            handler.channel.onmessage = null;
            handler.channel.onclose = null;
            handler.channel.onerror = null;
            handler.channel.close();
        }

        handler.pc.onicecandidate = null;
        handler.pc.onnegotiationneeded = null;
        handler.pc.ondatachannel = null;
        handler.pc.onconnectionstatechange = null;
        handler.pc.close();
    }
};

autoAddDeps(LibraryJsWebRtcPeer, '$jsWebRtcState');
autoAddDeps(LibraryJsWebRtcPeer, '$jsWebRtcCall');
mergeInto(LibraryManager.library, LibraryJsWebRtcPeer);
