using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Internal
{
    internal readonly struct AsepriteBakeSettings
    {
        private AsepriteBakeSettings(float pixelsPerUnit, Vector2 pivot, FilterMode filterMode)
        {
            PixelsPerUnit = pixelsPerUnit;
            Pivot = pivot;
            FilterMode = filterMode;
        }

        public float PixelsPerUnit { get; }
        public Vector2 Pivot { get; }
        public FilterMode FilterMode { get; }

        // Frames are baked over the whole canvas, so the importer pivot is relative to the canvas.
        public static AsepriteBakeSettings From(AssetImporter importer)
        {
            if (importer is not AsepriteImporter aseprite)
                return new AsepriteBakeSettings(100f, new Vector2(0.5f, 0f), FilterMode.Point);

            return new AsepriteBakeSettings(
                aseprite.spritePixelsPerUnit,
                ToPivot(aseprite.pivotAlignment, aseprite.customPivotPosition),
                aseprite.filterMode);
        }

        private static Vector2 ToPivot(SpriteAlignment alignment, Vector2 custom)
        {
            return alignment switch
            {
                SpriteAlignment.Center => new Vector2(0.5f, 0.5f),
                SpriteAlignment.TopLeft => new Vector2(0f, 1f),
                SpriteAlignment.TopCenter => new Vector2(0.5f, 1f),
                SpriteAlignment.TopRight => new Vector2(1f, 1f),
                SpriteAlignment.LeftCenter => new Vector2(0f, 0.5f),
                SpriteAlignment.RightCenter => new Vector2(1f, 0.5f),
                SpriteAlignment.BottomLeft => new Vector2(0f, 0f),
                SpriteAlignment.BottomCenter => new Vector2(0.5f, 0f),
                SpriteAlignment.BottomRight => new Vector2(1f, 0f),
                _ => custom
            };
        }
    }
}
