#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Internal
{
    // IWebRtcPeer on libdatachannel (Windows, Linux: the desktop players and editors). The library calls back from its
    // own threads: the events go to IWebRtcPeerEvents from there, WebRtcPeerSocket queues them for the main thread.
    public class NativeWebRtcPeer : IWebRtcPeer
    {
        private const int NoChannel = -1;
        private const int ChannelOfDisposedPeer = -2;

        public NativeWebRtcPeer(WebRtcPeerOptions options, IWebRtcPeerEvents events)
        {
            NativeWebRtcLibrary.Initialize();

            _events = events;
            _key = NativeWebRtcCallbacks.Register(this);

            _connection = CreateConnection(options);
            NativeWebRtcApi.SetUserPointer(_connection, (IntPtr)_key);

            Check(NativeWebRtcApi.SetLocalDescriptionCallback(_connection, NativeWebRtcCallbacks.OnDescriptionCallback), "description");
            Check(NativeWebRtcApi.SetLocalCandidateCallback(_connection, NativeWebRtcCallbacks.OnCandidateCallback), "candidate");
            Check(NativeWebRtcApi.SetStateChangeCallback(_connection, NativeWebRtcCallbacks.OnStateCallback), "state");
            Check(NativeWebRtcApi.SetDataChannelCallback(_connection, NativeWebRtcCallbacks.OnDataChannelCallback), "data channel");
        }

        private readonly IWebRtcPeerEvents _events;
        private readonly int _key;
        private readonly int _connection;

        // Set on the main thread (the offering side) or on a thread of the library (the answering side, the channel of
        // the peer); read on both. Dispose takes it with one exchange: of a channel and a Dispose that race, whichever
        // comes second sees the other and deletes the channel.
        private int _channel = NoChannel;

        // Once each: the open of a channel may be seen both by its callback and by the check after the callbacks are set.
        private int _isOpened;
        private int _isLost;
        private bool _isDisposed;

        public int MaxMessageSize => Check(NativeWebRtcApi.MaxMessageSize(Channel()), "max message size");

        public void CreateChannel(string label)
        {
            if (_channel >= 0)
                throw new InvalidOperationException($"[Network] [NativeWebRtc] The channel is already created: {label}");

            var channel = Check(NativeWebRtcApi.CreateDataChannel(_connection, NativeWebRtcApi.Utf8(label)),
                "create data channel");

            SetupChannel(channel);
        }

        public void SetRemoteDescription(string type, string sdp)
        {
            Check(NativeWebRtcApi.SetRemoteDescription(_connection, NativeWebRtcApi.Utf8(sdp), NativeWebRtcApi.Utf8(type)),
                "set remote description");
        }

        public void AddRemoteCandidate(string candidate, string mid)
        {
            Check(NativeWebRtcApi.AddRemoteCandidate(_connection, NativeWebRtcApi.Utf8(candidate), NativeWebRtcApi.Utf8(mid)),
                "add remote candidate");
        }

        public void Send(byte[] message)
        {
            Check(NativeWebRtcApi.SendMessage(Channel(), message, message.Length), "send");
        }

        public void Dispose()
        {
            if (_isDisposed == true)
                return;

            _isDisposed = true;

            // Out of the table first: a callback that is still running finds no peer and drops its event.
            NativeWebRtcCallbacks.Unregister(_key);

            var channel = Interlocked.Exchange(ref _channel, ChannelOfDisposedPeer);

            if (channel >= 0)
                NativeWebRtcApi.Delete(channel);

            NativeWebRtcApi.DeletePeerConnection(_connection);
        }

        internal IWebRtcPeerEvents Events => _events;

        private int Channel()
        {
            if (_channel < 0)
                throw new InvalidOperationException("[Network] [NativeWebRtc] The channel is not created");

            return _channel;
        }

        internal void SetupChannel(int channel)
        {
            var current = Interlocked.CompareExchange(ref _channel, channel, NoChannel);

            if (current != NoChannel)
            {
                // The library keeps a channel it handed over until it is deleted, and this peer will not hold it.
                Check(NativeWebRtcApi.Delete(channel), "delete a channel the peer does not keep");

                if (current == ChannelOfDisposedPeer)
                    return;

                throw new InvalidOperationException($"[Network] [NativeWebRtc] A second data channel: {channel}");
            }

            NativeWebRtcApi.SetUserPointer(channel, (IntPtr)_key);

            Check(NativeWebRtcApi.SetOpenCallback(channel, NativeWebRtcCallbacks.OnOpenCallback), "open");
            Check(NativeWebRtcApi.SetClosedCallback(channel, NativeWebRtcCallbacks.OnClosedCallback), "closed");
            Check(NativeWebRtcApi.SetErrorCallback(channel, NativeWebRtcCallbacks.OnErrorCallback), "error");
            Check(NativeWebRtcApi.SetMessageCallback(channel, NativeWebRtcCallbacks.OnMessageCallback), "message");

            // The channel of the peer may be open before its callback is set.
            if (NativeWebRtcApi.IsOpen(channel) == true)
                Opened();
        }

        internal void Opened()
        {
            if (Interlocked.Exchange(ref _isOpened, 1) == 0)
                _events.OnOpen();
        }

        internal void Lost(string reason)
        {
            if (Interlocked.Exchange(ref _isLost, 1) == 0)
                _events.OnLost(reason);
        }

        private int CreateConnection(WebRtcPeerOptions options)
        {
            var servers = new IntPtr[options.IceServers.Count];
            var serversHandle = default(GCHandle);

            try
            {
                for (var i = 0; i < servers.Length; i++)
                    servers[i] = Marshal.StringToCoTaskMemUTF8(options.IceServers[i]);

                serversHandle = GCHandle.Alloc(servers, GCHandleType.Pinned);

                var configuration = new NativeWebRtcApi.Configuration(
                    servers.Length > 0 ? serversHandle.AddrOfPinnedObject() : IntPtr.Zero,
                    servers.Length);

                // The library copies the configuration.
                return Check(NativeWebRtcApi.CreatePeerConnection(ref configuration), "create peer connection");
            }
            finally
            {
                if (serversHandle.IsAllocated == true)
                    serversHandle.Free();

                foreach (var server in servers)
                    Marshal.FreeCoTaskMem(server);
            }
        }

        private static int Check(int result, string call)
        {
            return NativeWebRtcApi.Check(result, call);
        }
    }
}
#endif
