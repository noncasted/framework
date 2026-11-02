using System.Collections.Generic;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Internal
{
    // Turns an open aseprite file into a document: image layers, tags and copies of the cells.
    internal static class AsepriteDocumentBuilder
    {
        private const string LogTag = "AsepriteDocument";

        internal static AsepriteDocument Build(AsepriteFile file, string assetPath)
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

            var cells = new Dictionary<(int Frame, int Layer), AsepriteCell>();

            foreach (var pair in rawCells)
                cells[pair.Key] = AsepriteCell.Copy(pair.Value);

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
    }
}
