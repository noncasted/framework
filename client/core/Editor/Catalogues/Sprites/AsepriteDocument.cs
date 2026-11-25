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

        private static readonly ILog _log = Logs.Of<AsepriteDocument>();

        private static readonly Dictionary<string, CachedDocument> Cache = new(StringComparer.Ordinal);

        private readonly Dictionary<(int Frame, int Layer), AsepriteCell> _cells;

        internal AsepriteDocument(
            int width,
            int height,
            IReadOnlyList<AsepriteLayer> layers,
            IReadOnlyList<AsepriteTag> tags,
            Dictionary<(int Frame, int Layer), AsepriteCell> cells)
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

            var document = ReadForInspector(assetPath);
            Cache[assetPath] = new CachedDocument(writeTime, document);
            return document;
        }

        public static AsepriteDocument Read(string assetPath)
        {
            // A failure to read throws: the baker would take it for a file without animations and delete its textures.
            var file = AsepriteReader.ReadFile(assetPath);

            if (file == null)
                throw new InvalidOperationException($"[{LogTag}] Failed to read {assetPath}");

            using (file)
                return AsepriteDocumentBuilder.Build(file, assetPath);
        }

        // The inspector is the boundary: it says that the file is broken and draws on.
        private static AsepriteDocument ReadForInspector(string assetPath)
        {
            try
            {
                return Read(assetPath);
            }
            catch (Exception exception)
            {
                _log.Error(exception, "Failed to read {Path}", assetPath);
                return null;
            }
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

                cell.BlendInto(pixels, Width, Height, layer.Opacity * cell.Opacity);
            }

            return pixels;
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
}
