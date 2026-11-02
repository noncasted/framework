using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Internal.EditorTests
{
    // Writes a small aseprite file by the format of the importer package: 32-bit colour, raw cells, no palette.
    internal sealed class AsepriteFileFixture
    {
        public const ushort Visible = 1;
        public const ushort Hidden = 0;

        private const ushort LayerChunk = 0x2004;
        private const ushort CellChunk = 0x2005;
        private const ushort TagsChunk = 0x2018;
        private const ushort NormalLayer = 0;
        private const ushort GroupLayer = 1;

        public AsepriteFileFixture(int width, int height, int frames)
        {
            _width = width;
            _height = height;
            _frames = new List<byte[]>[frames];

            for (var i = 0; i < frames; i++)
                _frames[i] = new List<byte[]>();
        }

        private readonly int _width;
        private readonly int _height;
        private readonly List<byte[]>[] _frames;

        public AsepriteFileFixture Layer(
            string name,
            ushort flags = Visible,
            int childLevel = 0,
            byte opacity = 255,
            ushort blendMode = 0)
        {
            return AddLayer(name, flags, NormalLayer, childLevel, opacity, blendMode);
        }

        public AsepriteFileFixture Group(string name, ushort flags = Visible, int childLevel = 0)
        {
            return AddLayer(name, flags, GroupLayer, childLevel, 255, 0);
        }

        // Pixels go row by row from the top, as in the file.
        public AsepriteFileFixture Cell(
            int frame,
            int layer,
            int x,
            int y,
            int width,
            int height,
            Color32[] pixels,
            byte opacity = 255)
        {
            var writer = Chunk(out var stream);
            WriteCellHeader(writer, layer, x, y, opacity, 0);
            writer.Write((ushort)width);
            writer.Write((ushort)height);

            foreach (var pixel in pixels)
            {
                writer.Write(pixel.r);
                writer.Write(pixel.g);
                writer.Write(pixel.b);
                writer.Write(pixel.a);
            }

            _frames[frame].Add(Close(CellChunk, writer, stream));
            return this;
        }

        public AsepriteFileFixture LinkedCell(int frame, int layer, int linkedToFrame)
        {
            var writer = Chunk(out var stream);
            WriteCellHeader(writer, layer, 0, 0, 255, 1);
            writer.Write((ushort)linkedToFrame);
            _frames[frame].Add(Close(CellChunk, writer, stream));
            return this;
        }

        public AsepriteFileFixture Tag(string name, int from, int to)
        {
            var writer = Chunk(out var stream);
            writer.Write((ushort)1);
            writer.Write(new byte[8]);
            writer.Write((ushort)from);
            writer.Write((ushort)to);
            writer.Write((byte)0);
            writer.Write((ushort)0);
            writer.Write(new byte[10]);
            WriteString(writer, name);
            _frames[0].Add(Close(TagsChunk, writer, stream));
            return this;
        }

        public void Write(string path)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(0u);
            writer.Write((ushort)0xA5E0);
            writer.Write((ushort)_frames.Length);
            writer.Write((ushort)_width);
            writer.Write((ushort)_height);
            writer.Write((ushort)32);
            writer.Write(1u);
            writer.Write((ushort)100);
            writer.Write(new byte[128 - stream.Position]);

            foreach (var chunks in _frames)
            {
                var size = 16;

                foreach (var chunk in chunks)
                    size += chunk.Length;

                writer.Write((uint)size);
                writer.Write((ushort)0xF1FA);
                writer.Write((ushort)chunks.Count);
                writer.Write((ushort)100);
                writer.Write((ushort)0);
                writer.Write((uint)chunks.Count);

                foreach (var chunk in chunks)
                    writer.Write(chunk);
            }

            writer.Flush();
            stream.Position = 0;
            writer.Write((uint)stream.Length);
            File.WriteAllBytes(path, stream.ToArray());
        }

        private AsepriteFileFixture AddLayer(
            string name,
            ushort flags,
            ushort type,
            int childLevel,
            byte opacity,
            ushort blendMode)
        {
            var writer = Chunk(out var stream);
            writer.Write(flags);
            writer.Write(type);
            writer.Write((ushort)childLevel);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(blendMode);
            writer.Write(opacity);
            writer.Write(new byte[3]);
            WriteString(writer, name);
            _frames[0].Add(Close(LayerChunk, writer, stream));
            return this;
        }

        private static void WriteCellHeader(BinaryWriter writer, int layer, int x, int y, byte opacity, ushort type)
        {
            writer.Write((ushort)layer);
            writer.Write((short)x);
            writer.Write((short)y);
            writer.Write(opacity);
            writer.Write(type);
            writer.Write((short)0);
            writer.Write(new byte[5]);
        }

        private static void WriteString(BinaryWriter writer, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static BinaryWriter Chunk(out MemoryStream stream)
        {
            stream = new MemoryStream();
            return new BinaryWriter(stream);
        }

        private static byte[] Close(ushort type, BinaryWriter writer, MemoryStream stream)
        {
            writer.Flush();

            using var result = new MemoryStream();
            using var resultWriter = new BinaryWriter(result);
            resultWriter.Write((uint)(stream.Length + 6));
            resultWriter.Write(type);
            resultWriter.Write(stream.ToArray());
            resultWriter.Flush();
            writer.Dispose();
            return result.ToArray();
        }
    }
}
