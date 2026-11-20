using System;
using UnityEngine;

namespace Internal
{
    public class NetworkOptions : EnvAsset
    {
        // How many times per second changed properties of network objects are sent to the server.
        [SerializeField, Min(1f)] private float _propertiesSyncRate = 60f;

        // STUN servers of the direct connections between players (WebRtcPeerSocket). No TURN: a pair that STUN does not
        // connect plays through the server.
        [SerializeField] private string[] _peerIceServers = { "stun:stun.l.google.com:19302", "stun:stun.cloudflare.com:3478" };

        // An attempt of a direct connection that does not open in this time has failed.
        [SerializeField, Min(1f)] private float _peerConnectTimeout = 5f;

        // The matches of several players are played on the client of one of them (the host): the server session of the
        // match is the control plane only. Off - every match is played on the server. One value for every client of a
        // build (the protocol hash keeps other builds out).
        [SerializeField] private bool _isPeerMatches;

        // Off: the guests play through the tunnel of the server only (a test of the fallback).
        [SerializeField] private bool _isPeerDirectAllowed = true;

        public float PropertiesSyncInterval => 1f / _propertiesSyncRate;

        public WebRtcPeerOptions PeerOptions => new(_peerIceServers, TimeSpan.FromSeconds(_peerConnectTimeout));
        public bool IsPeerMatches => _isPeerMatches;
        public bool IsPeerDirectAllowed => _isPeerDirectAllowed;
    }
}
