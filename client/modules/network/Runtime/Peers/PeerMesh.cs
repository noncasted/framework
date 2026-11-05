using System;
using System.Collections.Generic;

namespace Internal
{
    // The full mesh of a session without a host: every player is connected to every other one, each connection with
    // the channels of WebRtcPeerOptions. A pair negotiates a direct connection (the smaller index offers, the bigger one
    // answers); when it does not open, the pair goes through the tunnel of the server. A lost connection is tried again
    // after PeerMeshOptions.ReconnectDelay while the player is in the session.
    public class PeerMesh
    {
        public PeerMesh(ISessionControl control, WebRtcPeerOptions options, PeerMeshOptions meshOptions)
        {
            // A default struct has no delays: the retries would go every frame.
            if (meshOptions.ReconnectDelay <= TimeSpan.Zero || meshOptions.MaxReconnectDelay < meshOptions.ReconnectDelay)
                throw new ArgumentException("[Network] [Mesh] The reconnect delays of the options are not set", nameof(meshOptions));

            _control = control;
            _options = options;
            _meshOptions = meshOptions;
        }

        private readonly ISessionControl _control;
        private readonly WebRtcPeerOptions _options;
        private readonly PeerMeshOptions _meshOptions;
        private readonly Dictionary<int, PeerPair> _pairs = new();
        private readonly ViewableDictionary<int, IPeerLink> _links = new();
        private readonly ViewableProperty<int> _directLinks = new();
        private readonly ViewableProperty<int> _tunnelLinks = new();

        private ILifetime _lifetime;

        // By the index of the player in the server session. A link appears when its connection is open and the id of
        // the player is known; it goes away when the connection is lost or the player leaves.
        public IViewableDictionary<int, IPeerLink> Links => _links;

        // How many links go directly and how many through the server: the metric for the decision about TURN. A pair
        // that falls back to the tunnel logs why.
        public IViewableProperty<int> DirectLinks => _directLinks;
        public IViewableProperty<int> TunnelLinks => _tunnelLinks;

        public void Run(IReadOnlyLifetime lifetime)
        {
            if (_lifetime != null)
                throw new InvalidOperationException("[Network] [Mesh] Run is called twice");

            _lifetime = lifetime.Child();

            _control.Peers.Advise(_lifetime, OnPeerEntry);
            _control.Signaling.Incoming.Advise(_lifetime, OnOffer);
            _control.Tunnels.Incoming.Advise(_lifetime, OnTunnel);

            foreach (var peer in _control.Peers)
                OnPeerEntry(_control.Peers.GetLifetime(peer.Key), peer.Key, peer.Value);
        }

        private void OnPeerEntry(IReadOnlyLifetime entryLifetime, int index, Guid id)
        {
            // The pair may already exist: the first offer of the player came before this update.
            var pair = Pair(index);

            entryLifetime.Listen(() => OnPeerGone(index, pair));
            pair.SetRemoteId(id);
            pair.Start();
        }

        // A signal of a player without an open channel: the pair opens the channel inside the call and the signal goes
        // to it. The player may be unknown yet (its RemoteUpdate is on its way): the link waits for the id.
        private void OnOffer(int index)
        {
            Pair(index).OnOffer();
        }

        // The first frame of a tunnel: the other side of the pair chose the tunnel of the server.
        private void OnTunnel(int index)
        {
            Pair(index).OnTunnel();
        }

        private PeerPair Pair(int index)
        {
            if (_pairs.TryGetValue(index, out var pair) == true)
                return pair;

            pair = new PeerPair(_control, index, _options, _meshOptions, _lifetime, OnLink, OnLinkLost);
            _pairs.Add(index, pair);
            return pair;
        }

        private void OnPeerGone(int index, PeerPair pair)
        {
            if (_pairs.TryGetValue(index, out var current) == false || current != pair)
                return;

            _pairs.Remove(index);
            pair.PeerGone();
        }

        private void OnLink(IPeerLink link)
        {
            _links.Add(link.RemoteIndex, link);
            RefreshCounters();
        }

        private void OnLinkLost(IPeerLink link)
        {
            if (_links.TryGetValue(link.RemoteIndex, out var current) == false || current != link)
                throw new InvalidOperationException($"[Network] [Mesh] The link to {link.RemoteIndex} is not in the mesh");

            _links.Remove(link.RemoteIndex);
            RefreshCounters();
        }

        private void RefreshCounters()
        {
            var direct = 0;
            var tunnel = 0;

            foreach (var link in _links.Values)
            {
                if (link.Route == PeerRoute.Direct)
                    direct++;
                else
                    tunnel++;
            }

            _directLinks.Set(direct);
            _tunnelLinks.Set(tunnel);
        }
    }
}
