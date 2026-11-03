using UnityEngine;

namespace Internal
{
    // How the screen canvases the framework builds from code (the latency overlay of the network, the notice of an
    // outdated client) are drawn: the game says it once in the root scope, AddScreenCanvasCamera with the camera of its
    // UI or, explicitly, AddScreenCanvasOverlay. Nothing is assumed without it: a scope that builds such a canvas fails to
    // resolve. A Screen Space - Overlay canvas can lose its projection (iPadOS 18.2 Safari: the w column of the matrix
    // of the engine becomes zero after ~1100 frames and the UI vanishes), so a game that draws its UI through a camera
    // puts these canvases there too.
    public interface IScreenCanvasCamera
    {
        // Called on the root of a canvas built from code, after its hierarchy: puts the canvas and every object under it
        // where the game draws its screen canvases.
        void Attach(Canvas canvas);
    }
}
