using UnityEngine;

namespace Internal
{
    // The choice of a game that draws its screen canvases as Screen Space - Overlay (see IScreenCanvasCamera for the
    // platforms where the overlay loses its projection).
    public class ScreenCanvasOverlay : IScreenCanvasCamera
    {
        public void Attach(Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
