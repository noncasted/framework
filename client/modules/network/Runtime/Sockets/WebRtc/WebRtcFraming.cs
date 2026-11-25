using System;

namespace Internal
{
    // A data channel carries messages up to a size (MaxMessageSize of the peers, 64 KB taken as the most for every
    // browser), while a frame of the session may be larger (the schema of a floor, the entities of a seat). A frame
    // goes as parts, each with a one byte header: More - the frame goes on in the next part, Last - it ends here. The
    // channel is reliable and ordered, so the parts of a frame come together and in order.
    public static class WebRtcFraming
    {
        public const byte Last = 0;
        public const byte More = 1;

        public const int HeaderSize = 1;
        public const int MaxPartSize = 64 * 1024;

        // The largest payload of one part for a channel of maxMessageSize.
        public static int PayloadSize(int maxMessageSize)
        {
            if (maxMessageSize <= HeaderSize)
                throw new ArgumentOutOfRangeException(nameof(maxMessageSize), maxMessageSize, "No room for a payload");

            return Math.Min(maxMessageSize, MaxPartSize) - HeaderSize;
        }

        public static void Split(byte[] frame, int payloadSize, Action<byte[]> send)
        {
            var offset = 0;

            do
            {
                var count = Math.Min(payloadSize, frame.Length - offset);
                var isLast = offset + count == frame.Length;
                var part = new byte[HeaderSize + count];

                part[0] = isLast == true ? Last : More;
                Buffer.BlockCopy(frame, offset, part, HeaderSize, count);
                send(part);

                offset += count;
            }
            while (offset < frame.Length);
        }
    }
}
