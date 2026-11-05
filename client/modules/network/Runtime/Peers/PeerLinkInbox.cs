using System;
using System.Collections.Generic;

namespace Internal
{
    // The received frames of a connection of the mesh, by channel. The mesh publishes a link a pass or more after its
    // connection opens (Connect wakes up later, the id of the player may come later), and the other side may already
    // send: the frames of that gap are held and replayed right after the publication, so a subscriber that attaches
    // when the link appears in PeerMesh.Links gets every frame from the first one.
    internal sealed class PeerLinkInbox
    {
        public PeerLinkInbox(int channels, IReadOnlyLifetime lifetime)
        {
            _received = new ViewableDelegate<byte[]>[channels];

            for (var i = 0; i < _received.Length; i++)
                _received[i] = new ViewableDelegate<byte[]>();

            lifetime.Listen(Dispose);
        }

        private readonly ViewableDelegate<byte[]>[] _received;
        private readonly List<(int Channel, byte[] Frame)> _held = new();

        private bool _isReleased;

        public int Count => _received.Length;

        public IViewableDelegate<byte[]> Received(int channel)
        {
            return _received[channel];
        }

        public void Receive(int channel, byte[] frame)
        {
            if (_isReleased == false)
            {
                _held.Add((channel, frame));
                return;
            }

            _received[channel].Invoke(frame);
        }

        // The link is in the mesh and its subscribers are attached: the held frames go to them in their order.
        public void Release()
        {
            if (_isReleased == true)
                throw new InvalidOperationException("[Network] [Mesh] The frames of a link are released twice");

            _isReleased = true;

            foreach (var (channel, frame) in _held)
                _received[channel].Invoke(frame);

            _held.Clear();
        }

        private void Dispose()
        {
            _held.Clear();

            foreach (var received in _received)
                received.Dispose();
        }
    }
}
