using System;
using UnityEngine;

namespace Internal
{
    // The link of one pair on its way to the mesh: the connection of an attempt is ready (Prepare), the id of the
    // player is known (the offer may come before its RemoteUpdate), then the link is published; its loss is reported once.
    // The frames received before the publication wait in the inbox of the attempt and go to the subscribers that attached
    // when the link appeared.
    internal sealed class PeerLinkPublication
    {
        public PeerLinkPublication(int remoteIndex, Action<IPeerLink> onLink, Action<IPeerLink> onLost)
        {
            _remoteIndex = remoteIndex;
            _onLink = onLink;
            _onLost = onLost;
        }

        private readonly int _remoteIndex;
        private readonly Action<IPeerLink> _onLink;
        private readonly Action<IPeerLink> _onLost;

        private Func<Guid, IPeerLink> _factory;
        private PeerLinkInbox _inbox;
        private IPeerLink _link;

        // The connection of the attempt is open: its link is made once the id of the player is known.
        public void Prepare(Func<Guid, IPeerLink> factory, PeerLinkInbox inbox)
        {
            _factory = factory;
            _inbox = inbox;
        }

        public void TryPublish(Guid remoteId)
        {
            if (_factory == null || remoteId == Guid.Empty)
                return;

            var factory = _factory;
            var inbox = _inbox;
            _factory = null;
            _inbox = null;
            _link = factory(remoteId);
            Debug.Log($"[Network] [Mesh] Connected to {_remoteIndex} ({remoteId}): {_link.Route}");
            _onLink(_link);
            inbox.Release();
        }

        public void ReportLost()
        {
            if (_link == null)
                return;

            var link = _link;
            _link = null;
            _onLost(link);
        }

        // The attempt has ended: a link not published yet is never made.
        public void Clear()
        {
            _factory = null;
            _inbox = null;
        }
    }
}
