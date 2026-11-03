using System;
using UnityEngine;

namespace Internal
{
    public static class ScreenCanvasExtensions
    {
        // The screen canvases of the framework drawn by the UI camera of the game (in the root scope: the network and
        // the ui modules build them in the scopes below).
        public static IScopeBuilder AddScreenCanvasCamera(
            this IScopeBuilder builder,
            Func<Camera> camera,
            string layer,
            float planeDistance)
        {
            builder.RegisterInstance(new ScreenCanvasCamera(camera, layer, planeDistance))
                   .As<IScreenCanvasCamera>();

            return builder;
        }

        // The screen canvases of the framework as Screen Space - Overlay: the explicit choice of a game without a UI camera.
        public static IScopeBuilder AddScreenCanvasOverlay(this IScopeBuilder builder)
        {
            builder.RegisterInstance(new ScreenCanvasOverlay())
                   .As<IScreenCanvasCamera>();

            return builder;
        }
    }
}
