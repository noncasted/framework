using System;
using System.IO;

namespace Internal
{
    // The receiving side: parts in, whole frames out.
    public class WebRtcFrameAssembler
    {
        private readonly MemoryStream _pending = new();

        // The frame the part completes, null while it goes on.
        public byte[] Add(byte[] part)
        {
            if (part.Length < WebRtcFraming.HeaderSize)
                throw new InvalidDataException("[Network] [WebRtc] A part without a header");

            var header = part[0];

            if (header != WebRtcFraming.Last && header != WebRtcFraming.More)
                throw new InvalidDataException($"[Network] [WebRtc] Unknown part header: {header}");

            // The common case: a frame in one part.
            if (header == WebRtcFraming.Last && _pending.Length == 0)
                return part.AsSpan(WebRtcFraming.HeaderSize).ToArray();

            _pending.Write(part, WebRtcFraming.HeaderSize, part.Length - WebRtcFraming.HeaderSize);

            if (header == WebRtcFraming.More)
                return null;

            var frame = _pending.ToArray();
            _pending.SetLength(0);
            return frame;
        }
    }
}
