using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Internal
{
    // Snapshot of an aseprite file for the catalog: tags, layers and cell pixels. A frame is composed
    // from an arbitrary set of layers, so one animation can turn off, for example, the hands.
    [NoAutoStaticsCleanup]
    public sealed class AsepriteDocument
    {
        private const string LogTag = "AsepriteDocument";

        private static readonly Dictionary<string, CachedDocument> Cache = new(StringComparer.Ordinal);

        private readonly Dictionary<(int Frame, int Layer), Cell> _cells;

        private AsepriteDocument(
            int width,
            int height,
            IReadOnlyList<AsepriteLayer> layers,
            IReadOnlyList<AsepriteTag> tags,
            Dictionary<(int Frame, int Layer), Cell> cells)
        {
            Width = width;
            Height = height;
            Layers = layers;
            Tags = tags;
            _cells = cells;
        }

        public int Width { get; }
        public int Height { get; }

        // Only layers with an image, ordered bottom to top.
        public IReadOnlyList<AsepriteLayer> Layers { get; }
        public IReadOnlyList<AsepriteTag> Tags { get; }

        // The inspector is drawn every frame, so the file is re-read only when it changes on disk.
        public static AsepriteDocument ReadCached(string assetPath)
        {
            var writeTime = File.GetLastWriteTimeUtc(assetPath);

            if (Cache.TryGetValue(assetPath, out var cached) && cached.WriteTime == writeTime)
                return cached.Document;

            var document = Read(assetPath);
            Cache[assetPath] = new CachedDocument(writeTime, document);
            return document;
        }

        public static AsepriteDocument Read(string assetPath)
        {
            AsepriteFile file;

            try
            {
                file = AsepriteReader.ReadFile(assetPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to read {assetPath}: {exception}");
                return null;
            }

            if (file == null)
                return null;

            using (file)
                return Build(file, assetPath);
        }

        public AsepriteTag FindTag(string name)
        {
            foreach (var tag in Tags)
            {
                if (string.Equals(tag.Name, name, StringComparison.Ordinal))
                    return tag;
            }

            return null;
        }

        public List<string> GetVisibleLayerNames()
        {
            var names = new List<string>();

            foreach (var layer in Layers)
            {
                if (layer.Visible)
                    names.Add(layer.Name);
            }

            return names;
        }

        // Frame pixels the size of the canvas, bottom to top, as Texture2D expects.
        public Color32[] Compose(int frame, IReadOnlyCollection<string> layerNames)
        {
            var pixels = new Color32[Width * Height];

            foreach (var layer in Layers)
            {
                if (Contains(layerNames, layer.Name) == false)
                    continue;

                if (_cells.TryGetValue((frame, layer.Index), out var cell) == false)
                    continue;

                Blend(pixels, cell, layer.Opacity * cell.Opacity);
            }

            return pixels;
        }

        private static AsepriteDocument Build(AsepriteFile file, string assetPath)
        {
            var layers = new List<AsepriteLayer>();
            var tags = new List<AsepriteTag>();
            var rawCells = new Dictionary<(int Frame, int Layer), CellChunk>();
            var links = new Dictionary<(int Frame, int Layer), int>();
            var allLayers = new List<LayerChunk>();

            for (var frame = 0; frame < file.frameData.Count; frame++)
            {
                foreach (var chunk in file.frameData[frame].chunks)
                {
                    switch (chunk)
                    {
                        case LayerChunk layer:
                            allLayers.Add(layer);
                            break;
                        case TagsChunk tagsChunk:
                            foreach (var tag in tagsChunk.tagData)
                                tags.Add(new AsepriteTag(tag.name, tag.fromFrame, tag.toFrame));
                            break;
                        case CellChunk cell when cell.cellType == CellTypes.LinkedCell:
                            links[(frame, cell.layerIndex)] = cell.linkedToFrame;
                            break;
                        case CellChunk cell when cell.cellType is CellTypes.RawImage or CellTypes.CompressedImage:
                            rawCells[(frame, cell.layerIndex)] = cell;
                            break;
                    }
                }
            }

            CollectImageLayers(allLayers, layers, assetPath);

            var cells = new Dictionary<(int Frame, int Layer), Cell>();

            foreach (var pair in rawCells)
                cells[pair.Key] = Cell.Copy(pair.Value);

            foreach (var pair in links)
            {
                if (cells.TryGetValue((pair.Value, pair.Key.Layer), out var linked))
                    cells[pair.Key] = linked;
            }

            return new AsepriteDocument(file.width, file.height, layers, tags, cells);
        }

        // Groups define visibility and the name path but have no pixels of their own.
        private static void CollectImageLayers(
            IReadOnlyList<LayerChunk> allLayers,
            List<AsepriteLayer> layers,
            string assetPath)
        {
            var parents = new List<LayerChunk>();

            for (var index = 0; index < allLayers.Count; index++)
            {
                var layer = allLayers[index];

                while (parents.Count > layer.childLevel)
                    parents.RemoveAt(parents.Count - 1);

                var visible = (layer.flags & LayerFlags.Visible) != 0;
                var path = layer.name;

                foreach (var parent in parents)
                    visible &= (parent.flags & LayerFlags.Visible) != 0;

                for (var i = parents.Count - 1; i >= 0; i--)
                    path = parents[i].name + "/" + path;

                if (layer.layerType == LayerTypes.Group)
                {
                    parents.Add(layer);
                    continue;
                }

                if (layer.layerType != LayerTypes.Normal)
                    continue;

                if (layer.blendMode != BlendModes.Normal)
                    Debug.LogWarning(
                        $"[{LogTag}] Layer '{path}' in {assetPath} uses {layer.blendMode} blending, composed as Normal.");

                layers.Add(new AsepriteLayer(index, path, visible, layer.opacity / 255f));
            }
        }

        private void Blend(Color32[] pixels, Cell cell, float opacity)
        {
            for (var y = 0; y < cell.Height; y++)
            {
                var canvasY = cell.Y + y;

                if (canvasY < 0 || canvasY >= Height)
                    continue;

                // Aseprite counts rows from the top, a Unity texture from the bottom.
                var row = (Height - 1 - canvasY) * Width;

                for (var x = 0; x < cell.Width; x++)
                {
                    var canvasX = cell.X + x;

                    if (canvasX < 0 || canvasX >= Width)
                        continue;

                    var source = cell.Pixels[y * cell.Width + x];
                    var sourceAlpha = source.a / 255f * opacity;

                    if (sourceAlpha <= 0f)
                        continue;

                    var target = pixels[row + canvasX];
                    var targetAlpha = target.a / 255f;
                    var alpha = sourceAlpha + targetAlpha * (1f - sourceAlpha);
                    var targetWeight = targetAlpha * (1f - sourceAlpha);

                    pixels[row + canvasX] = new Color32(
                            Mix(source.r, target.r, sourceAlpha, targetWeight, alpha),
                            Mix(source.g, target.g, sourceAlpha, targetWeight, alpha),
                            Mix(source.b, target.b, sourceAlpha, targetWeight, alpha),
                            (byte)Mathf.RoundToInt(alpha * 255f)
                        );
                }
            }
        }

        private static byte Mix(byte source, byte target, float sourceWeight, float targetWeight, float alpha)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt((source * sourceWeight + target * targetWeight) / alpha), 0, 255);
        }

        private static bool Contains(IReadOnlyCollection<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private sealed class Cell
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public float Opacity;
            public Color32[] Pixels;

            // The cell NativeArray lives only while AsepriteFile is open, so the pixels are copied.
            public static Cell Copy(CellChunk chunk)
            {
                return new Cell
                {
                    X = chunk.posX,
                    Y = chunk.posY,
                    Width = chunk.width,
                    Height = chunk.height,
                    Opacity = chunk.opacity / 255f,
                    Pixels = chunk.image.IsCreated ? chunk.image.ToArray() : new Color32[chunk.width * chunk.height]
                };
            }
        }

        private readonly struct CachedDocument
        {
            public CachedDocument(DateTime writeTime, AsepriteDocument document)
            {
                WriteTime = writeTime;
                Document = document;
            }

            public DateTime WriteTime { get; }
            public AsepriteDocument Document { get; }
        }
    }

    public sealed class AsepriteLayer
    {
        public AsepriteLayer(int index, string name, bool visible, float opacity)
        {
            Index = index;
            Name = name;
            Visible = visible;
            Opacity = opacity;
        }

        public int Index { get; }
        public string Name { get; }
        public bool Visible { get; }
        public float Opacity { get; }
    }

    public sealed class AsepriteTag
    {
        public AsepriteTag(string name, int from, int to)
        {
            Name = name;
            From = from;
            To = to;
        }

        public string Name { get; }
        public int From { get; }
        public int To { get; }
        public int FrameCount => To - From + 1;
    }
}
