using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Internal
{
    // Pixels of one layer in one frame, blended onto the canvas of a composed frame.
    internal sealed class AsepriteCell
    {
        private AsepriteCell(int x, int y, int width, int height, float opacity, Color32[] pixels)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Opacity = opacity;
            Pixels = pixels;
        }

        internal float Opacity { get; }

        private int X { get; }
        private int Y { get; }
        private int Width { get; }
        private int Height { get; }
        private Color32[] Pixels { get; }

        // The cell NativeArray lives only while AsepriteFile is open, so the pixels are copied.
        internal static AsepriteCell Copy(CellChunk chunk)
        {
            return new AsepriteCell(
                chunk.posX,
                chunk.posY,
                chunk.width,
                chunk.height,
                chunk.opacity / 255f,
                chunk.image.IsCreated ? chunk.image.ToArray() : new Color32[chunk.width * chunk.height]);
        }

        internal void BlendInto(Color32[] pixels, int canvasWidth, int canvasHeight, float opacity)
        {
            for (var y = 0; y < Height; y++)
            {
                var canvasY = Y + y;

                if (canvasY < 0 || canvasY >= canvasHeight)
                    continue;

                // Aseprite counts rows from the top, a Unity texture from the bottom.
                var row = (canvasHeight - 1 - canvasY) * canvasWidth;

                for (var x = 0; x < Width; x++)
                {
                    var canvasX = X + x;

                    if (canvasX < 0 || canvasX >= canvasWidth)
                        continue;

                    var source = Pixels[y * Width + x];
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
    }
}
