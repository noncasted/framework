using System.IO;

namespace Internal
{
    // Decodes one LZ4 block (the format of LZ4 and LZ4HC, no frame): the compressed table of contents of an archive.
    internal static class Lz4Block
    {
        public static byte[] Decode(byte[] source, int size)
        {
            var target = new byte[size];
            var read = 0;
            var written = 0;

            while (read < source.Length)
            {
                var token = source[read++];
                var literals = Length(source, ref read, token >> 4);

                Check(read + literals <= source.Length && written + literals <= size);
                System.Array.Copy(source, read, target, written, literals);
                read += literals;
                written += literals;

                // The last sequence has literals only.
                if (read >= source.Length)
                    break;

                Check(read + 2 <= source.Length);
                var distance = source[read] | (source[read + 1] << 8);
                read += 2;
                var match = Length(source, ref read, token & 15) + 4;

                Check(distance > 0 && distance <= written && written + match <= size);

                // Byte by byte: a match may overlap the bytes it writes.
                for (var i = 0; i < match; i++, written++)
                    target[written] = target[written - distance];
            }

            if (written != size)
                throw new InvalidDataException($"An LZ4 block decoded into {written} bytes, not {size}");

            return target;
        }

        private static int Length(byte[] source, ref int read, int length)
        {
            if (length != 15)
                return length;

            byte next;

            do
            {
                Check(read < source.Length);
                next = source[read++];
                length += next;
            }
            while (next == 255);

            return length;
        }

        private static void Check(bool condition)
        {
            if (condition == false)
                throw new InvalidDataException("A broken LZ4 block");
        }
    }
}
