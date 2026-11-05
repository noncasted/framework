using System;
using System.Collections.Generic;

namespace Internal
{
    // A platform peer connection with one reliable ordered data channel: the browser RTCPeerConnection (JsWebRtcPeer)
    // or libdatachannel (NativeWebRtcPeer). Raw: no framing, no threads policy - the events come through
    // IWebRtcPeerEvents from any thread, WebRtcPeerSocket brings them to the main thread.
    public interface IWebRtcPeer : IDisposable
    {
        // The largest message the channel carries (the smaller of both sides), known once it is open.
        int MaxMessageSize { get; }

        // The offering side: creates the channel, the negotiation starts with a local offer.
        void CreateChannel(string label);

        void SetRemoteDescription(string type, string sdp);
        void AddRemoteCandidate(string candidate, string mid);

        // A channel that is not open fails loudly.
        void Send(byte[] message);
    }

    public interface IWebRtcPeerEvents
    {
        void OnLocalDescription(string type, string sdp);
        void OnLocalCandidate(string candidate, string mid);
        void OnOpen();
        void OnMessage(byte[] bytes);

        // The channel or the connection is lost (closed by the peer, ICE failed or disconnected, an error). Once.
        void OnLost(string reason);
    }

    public readonly struct WebRtcPeerOptions
    {
        public WebRtcPeerOptions(IReadOnlyList<string> iceServers, TimeSpan connectTimeout)
            : this(iceServers, connectTimeout, SingleChannel)
        {
        }

        public WebRtcPeerOptions(
            IReadOnlyList<string> iceServers,
            TimeSpan connectTimeout,
            IReadOnlyList<WebRtcChannelSpec> channels)
        {
            if (channels == null)
                throw new ArgumentNullException(nameof(channels));

            if (channels.Count == 0)
                throw new ArgumentException("[Network] [WebRtc] A connection declares at least one channel", nameof(channels));

            var labels = new HashSet<string>();

            foreach (var channel in channels)
            {
                if (string.IsNullOrEmpty(channel.Label) == true)
                    throw new ArgumentException("[Network] [WebRtc] A channel has no label", nameof(channels));

                if (labels.Add(channel.Label) == false)
                    throw new ArgumentException($"[Network] [WebRtc] Two channels carry the label '{channel.Label}'", nameof(channels));
            }

            IceServers = iceServers;
            ConnectTimeout = connectTimeout;
            Channels = channels;
        }

        private static readonly IReadOnlyList<WebRtcChannelSpec> SingleChannel =
            Array.AsReadOnly(new[] { new WebRtcChannelSpec("session", WebRtcChannelMode.ReliableOrdered) });

        // "stun:host:port" urls. Empty: host candidates only (a local network, a test inside one process).
        public readonly IReadOnlyList<string> IceServers;

        // An attempt that does not open the channels in this time has failed (the caller falls back to the server).
        public readonly TimeSpan ConnectTimeout;

        // The channels of the connection, declared the same on both sides: a channel is addressed by its index here.
        public readonly IReadOnlyList<WebRtcChannelSpec> Channels;
    }

    public static class WebRtcPeers
    {
        // The platforms with a peer: the browser, and the ones libdatachannel is built for (Runtime/Plugins). Elsewhere
        // Create fails with DllNotFoundException.
        public static bool IsSupported(UnityEngine.RuntimePlatform platform)
        {
            return platform switch
            {
                UnityEngine.RuntimePlatform.WebGLPlayer => true,
                UnityEngine.RuntimePlatform.LinuxEditor => true,
                UnityEngine.RuntimePlatform.LinuxPlayer => true,
                UnityEngine.RuntimePlatform.WindowsEditor => true,
                UnityEngine.RuntimePlatform.WindowsPlayer => true,
                _ => false
            };
        }

        public static IWebRtcPeer Create(WebRtcPeerOptions options, IWebRtcPeerEvents events)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new JsWebRtcPeer(options, events);
#else
            return new NativeWebRtcPeer(options, events);
#endif
        }

        // The connection with every channel of the options: the desktop peer carries them all, the browser one channel
        // (SingleChannelWebRtcPeer), which is all a link of one channel asks for.
        public static IWebRtcChannels CreateChannels(WebRtcPeerOptions options, IWebRtcChannelEvents events)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new SingleChannelWebRtcPeer(options, events);
#else
            return new NativeWebRtcPeer(options, events);
#endif
        }
    }
}
