using System;
using System.Collections.Generic;

namespace Internal
{
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
}
