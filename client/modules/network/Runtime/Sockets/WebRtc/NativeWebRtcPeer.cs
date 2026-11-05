#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Internal
{
    // IWebRtcPeer and IWebRtcChannels on libdatachannel (Windows, Linux: the desktop players and editors). The library
    // calls back from its own threads: the events go to the events object from there, WebRtcPeerLink and WebRtcPeerSocket
    // queue them for the main thread. A peer created with IWebRtcPeerEvents carries one channel (IWebRtcPeer), one
    // created with IWebRtcChannelEvents carries every channel of the options (IWebRtcChannels).
    public class NativeWebRtcPeer : IWebRtcPeer, IWebRtcChannels
    {
        private const int NoChannel = -1;

        public NativeWebRtcPeer(WebRtcPeerOptions options, IWebRtcPeerEvents events) : this(options)
        {
            _events = events;
            _channels = new int[1];
            _opened = new bool[1];
            _channels[0] = NoChannel;
        }

        public NativeWebRtcPeer(WebRtcPeerOptions options, IWebRtcChannelEvents events) : this(options)
        {
            _channelEvents = events;
            _specs = options.Channels;
            _channels = new int[options.Channels.Count];
            _opened = new bool[options.Channels.Count];

            for (var i = 0; i < _channels.Length; i++)
                _channels[i] = NoChannel;
        }

        private NativeWebRtcPeer(WebRtcPeerOptions options)
        {
            NativeWebRtcLibrary.Initialize();

            _key = NativeWebRtcCallbacks.Register(this);

            _connection = CreateConnection(options);
            NativeWebRtcApi.SetUserPointer(_connection, (IntPtr)_key);

            Check(NativeWebRtcApi.SetLocalDescriptionCallback(_connection, NativeWebRtcCallbacks.OnDescriptionCallback), "description");
            Check(NativeWebRtcApi.SetLocalCandidateCallback(_connection, NativeWebRtcCallbacks.OnCandidateCallback), "candidate");
            Check(NativeWebRtcApi.SetStateChangeCallback(_connection, NativeWebRtcCallbacks.OnStateCallback), "state");
            Check(NativeWebRtcApi.SetDataChannelCallback(_connection, NativeWebRtcCallbacks.OnDataChannelCallback), "data channel");
        }

        private readonly IWebRtcPeerEvents _events;
        private readonly IWebRtcChannelEvents _channelEvents;
        private readonly IReadOnlyList<WebRtcChannelSpec> _specs;
        private readonly int _key;
        private readonly int _connection;

        // The channel ids by the index of the spec, NoChannel until the channel is created. Set on the main thread (the
        // offering side) or on a thread of the library (the answering side), read on both.
        private readonly int[] _channels;
        private readonly bool[] _opened;

        // The bookkeeping of the channels: of a channel and a Dispose that race, whichever comes second sees the other
        // and deletes the channel.
        private readonly object _gate = new();

        private bool _isDisposed;

        // Once: a channel closed and the state change of the connection both report the loss.
        private int _isLost;

        public int MaxMessageSize => Check(NativeWebRtcApi.MaxMessageSize(ChannelId(0)), "max message size");

        public void CreateChannel(string label)
        {
            if (_channelEvents != null)
                throw new InvalidOperationException("[Network] [NativeWebRtc] The peer carries several channels: CreateChannels");

            lock (_gate)
            {
                ThrowIfDisposed();

                if (_channels[0] != NoChannel)
                    throw new InvalidOperationException($"[Network] [NativeWebRtc] The channel is already created: {label}");
            }

            var channel = Check(NativeWebRtcApi.CreateDataChannel(_connection, NativeWebRtcApi.Utf8(label)),
                "create data channel");

            SetupChannel(channel);
        }

        public void CreateChannels()
        {
            if (_channelEvents == null)
                throw new InvalidOperationException("[Network] [NativeWebRtc] The peer carries one channel: CreateChannel");

            lock (_gate)
            {
                ThrowIfDisposed();

                for (var i = 0; i < _channels.Length; i++)
                {
                    if (_channels[i] != NoChannel)
                        throw new InvalidOperationException("[Network] [NativeWebRtc] The channels are already created");
                }
            }

            // All of them before the negotiation starts: the first one raises it.
            for (var i = 0; i < _specs.Count; i++)
            {
                var init = NativeWebRtcApi.DataChannelInit.Of(_specs[i].Mode);
                var channel = Check(NativeWebRtcApi.CreateDataChannelEx(_connection, NativeWebRtcApi.Utf8(_specs[i].Label), ref init),
                    $"create data channel '{_specs[i].Label}'");

                SetupChannel(channel);
            }
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
            Send(0, message);
        }

        public void Send(int channel, byte[] message)
        {
            Check(NativeWebRtcApi.SendMessage(ChannelId(channel), message, message.Length), "send");
        }

        // IWebRtcChannels: the property of IWebRtcPeer is the channel 0, and C# keeps one name per type.
        int IWebRtcChannels.MaxMessageSize(int channel)
        {
            return Check(NativeWebRtcApi.MaxMessageSize(ChannelId(channel)), "max message size");
        }

        public int BufferedAmount(int channel)
        {
            return Check(NativeWebRtcApi.GetBufferedAmount(ChannelId(channel)), "buffered amount");
        }

        public void Dispose()
        {
            int[] channels;

            lock (_gate)
            {
                if (_isDisposed == true)
                    return;

                _isDisposed = true;
                channels = (int[])_channels.Clone();

                for (var i = 0; i < _channels.Length; i++)
                    _channels[i] = NoChannel;
            }

            // Out of the table first: a callback that is still running finds no peer and drops its event.
            NativeWebRtcCallbacks.Unregister(_key);

            foreach (var channel in channels)
            {
                if (channel >= 0)
                    NativeWebRtcApi.Delete(channel);
            }

            NativeWebRtcApi.DeletePeerConnection(_connection);
        }

        internal void LocalDescription(string type, string sdp)
        {
            if (_channelEvents != null)
                _channelEvents.OnLocalDescription(type, sdp);
            else
                _events.OnLocalDescription(type, sdp);
        }

        internal void LocalCandidate(string candidate, string mid)
        {
            if (_channelEvents != null)
                _channelEvents.OnLocalCandidate(candidate, mid);
            else
                _events.OnLocalCandidate(candidate, mid);
        }

        // The library hands over a channel of the peer: the index comes from its label, the label of the offer of the
        // other side.
        internal void SetupChannel(int channel)
        {
            var index = ResolveIndex(channel);

            if (index < 0)
                return;

            var isAccepted = false;
            var isDuplicate = false;

            lock (_gate)
            {
                if (_isDisposed == false && _channels[index] == NoChannel)
                {
                    _channels[index] = channel;
                    isAccepted = true;
                }
                else if (_isDisposed == false)
                {
                    isDuplicate = true;
                }
            }

            if (isAccepted == false)
            {
                // The library keeps a channel it handed over until it is deleted, and this peer will not hold it.
                NativeWebRtcApi.Check(NativeWebRtcApi.Delete(channel), "delete a channel the peer does not keep");

                if (isDuplicate == false)
                    return;

                if (_channelEvents != null)
                {
                    Lost($"duplicate channel label: {_specs[index].Label}");
                    return;
                }

                throw new InvalidOperationException($"[Network] [NativeWebRtc] A second data channel: {channel}");
            }

            NativeWebRtcApi.SetUserPointer(channel, (IntPtr)_key);

            Check(NativeWebRtcApi.SetOpenCallback(channel, NativeWebRtcCallbacks.OnOpenCallback), "open");
            Check(NativeWebRtcApi.SetClosedCallback(channel, NativeWebRtcCallbacks.OnClosedCallback), "closed");
            Check(NativeWebRtcApi.SetErrorCallback(channel, NativeWebRtcCallbacks.OnErrorCallback), "error");
            Check(NativeWebRtcApi.SetMessageCallback(channel, NativeWebRtcCallbacks.OnMessageCallback), "message");

            // The channel of the peer may be open before its callback is set.
            if (NativeWebRtcApi.IsOpen(channel) == true)
                Opened(channel);
        }

        internal void Opened(int channel)
        {
            var index = IndexOf(channel);

            if (index < 0)
                return;

            var isAllOpened = false;

            lock (_gate)
            {
                if (_opened[index] == true)
                    return;

                _opened[index] = true;
                isAllOpened = true;

                for (var i = 0; i < _opened.Length; i++)
                {
                    if (_opened[i] == false)
                    {
                        isAllOpened = false;
                        break;
                    }
                }
            }

            if (isAllOpened == false)
                return;

            if (_channelEvents != null)
                _channelEvents.OnOpen();
            else
                _events.OnOpen();
        }

        internal void Message(int channel, byte[] bytes)
        {
            if (_channelEvents == null)
            {
                _events.OnMessage(bytes);
                return;
            }

            var index = IndexOf(channel);

            if (index < 0)
                return;

            _channelEvents.OnMessage(index, bytes);
        }

        internal void Lost(string reason)
        {
            if (Interlocked.Exchange(ref _isLost, 1) == 0)
            {
                if (_channelEvents != null)
                    _channelEvents.OnLost(reason);
                else
                    _events.OnLost(reason);
            }
        }

        private int ResolveIndex(int channel)
        {
            // One channel: any label of the peer is the channel.
            if (_channelEvents == null)
                return 0;

            var label = ChannelLabel(channel);

            for (var i = 0; i < _specs.Count; i++)
            {
                if (_specs[i].Label == label)
                    return i;
            }

            // A peer of another version or a bug: the whole connection is lost, as it is when a channel closes.
            NativeWebRtcApi.Check(NativeWebRtcApi.Delete(channel), "delete a channel of an unknown label");
            Lost($"unknown channel label: {label}");
            return -1;
        }

        private static string ChannelLabel(int channel)
        {
            var size = Check(NativeWebRtcApi.GetDataChannelLabel(channel, IntPtr.Zero, 0), "channel label size");
            var buffer = Marshal.AllocHGlobal(size);

            try
            {
                Check(NativeWebRtcApi.GetDataChannelLabel(channel, buffer, size), "channel label");
                return Marshal.PtrToStringUTF8(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private int IndexOf(int channel)
        {
            var channels = _channels;

            for (var i = 0; i < channels.Length; i++)
            {
                if (Volatile.Read(ref channels[i]) == channel)
                    return i;
            }

            return -1;
        }

        private int ChannelId(int channel)
        {
            var channels = _channels;

            if ((uint)channel >= (uint)channels.Length)
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "[Network] [NativeWebRtc] No such channel");

            var id = Volatile.Read(ref channels[channel]);

            if (id < 0)
                throw new InvalidOperationException($"[Network] [NativeWebRtc] The channel {channel} is not created");

            return id;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed == true)
                throw new InvalidOperationException("[Network] [NativeWebRtc] The peer is disposed");
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
