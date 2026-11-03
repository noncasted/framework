using System;
using UnityEngine;

namespace Internal
{
    // The screen canvases built from code go to Screen Space - Camera: the camera of the UI of the game, the layer it
    // sees, the distance of the canvas plane between its clip planes. The camera is asked on every attach: the game may
    // create it on the first canvas and recreate it after a destroy (the end of play mode in the editor).
    public class ScreenCanvasCamera : IScreenCanvasCamera
    {
        public ScreenCanvasCamera(Func<Camera> camera, string layer, float planeDistance)
        {
            _layer = LayerMask.NameToLayer(layer);

            if (_layer < 0)
                throw new InvalidOperationException($"[UI] The screen canvas layer {layer} is not in the tags and layers of the project");

            if (planeDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(planeDistance), planeDistance, "[UI] The canvas plane is in front of the camera");

            _camera = camera;
            _layerName = layer;
            _planeDistance = planeDistance;
        }

        private readonly Func<Camera> _camera;
        private readonly int _layer;
        private readonly string _layerName;
        private readonly float _planeDistance;

        public void Attach(Canvas canvas)
        {
            var camera = _camera();

            if ((camera.cullingMask & (1 << _layer)) == 0)
                throw new InvalidOperationException($"[UI] {camera.name} does not see the screen canvas layer {_layerName}");

            if (_planeDistance <= camera.nearClipPlane || _planeDistance >= camera.farClipPlane)
                throw new InvalidOperationException(
                    $"[UI] The canvas plane {_planeDistance} is outside the clip planes of {camera.name} ({camera.nearClipPlane}..{camera.farClipPlane})");

            foreach (var transform in canvas.GetComponentsInChildren<Transform>(true))
                transform.gameObject.layer = _layer;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = _planeDistance;
        }
    }
}
