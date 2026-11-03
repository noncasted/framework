using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    // A screen canvas built from code goes where the game draws its screen canvases: Screen Space - Camera with the
    // camera of the game and on the layer it sees, or the overlay the game chose explicitly; a camera that cannot draw
    // the canvas throws.
    public class ScreenCanvasCameraTests
    {
        // The built-in UI layer: in every project.
        private const string Layer = "UI";
        private const float PlaneDistance = 1f;

        private GameObject _camera;
        private GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("ScreenCanvasCameraTests.Camera");
            var camera = _camera.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << LayerMask.NameToLayer(Layer);
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 1.5f;

            _canvas = new GameObject("ScreenCanvasCameraTests.Canvas", typeof(RectTransform));
            _canvas.AddComponent<Canvas>();
            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(_canvas.transform, false);
            var inactive = new GameObject("Inactive", typeof(RectTransform));
            inactive.transform.SetParent(child.transform, false);
            inactive.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_camera);
        }

        [Test]
        public void Attach_TakesTheCameraTheLayerAndThePlane()
        {
            var camera = _camera.GetComponent<Camera>();
            var canvas = _canvas.GetComponent<Canvas>();

            new ScreenCanvasCamera(() => camera, Layer, PlaneDistance).Attach(canvas);

            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.AreSame(camera, canvas.worldCamera);
            Assert.AreEqual(PlaneDistance, canvas.planeDistance);

            foreach (var transform in _canvas.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(LayerMask.NameToLayer(Layer), transform.gameObject.layer, transform.name);
        }

        [Test]
        public void Attach_AsksTheCameraEachTime()
        {
            var canvas = _canvas.GetComponent<Canvas>();
            var first = _camera.GetComponent<Camera>();
            var current = first;
            var screenCanvas = new ScreenCanvasCamera(() => current, Layer, PlaneDistance);

            screenCanvas.Attach(canvas);
            Object.DestroyImmediate(_camera);
            _camera = new GameObject("ScreenCanvasCameraTests.Recreated");
            current = _camera.AddComponent<Camera>();
            current.enabled = false;
            current.cullingMask = 1 << LayerMask.NameToLayer(Layer);
            screenCanvas.Attach(canvas);

            Assert.AreSame(current, canvas.worldCamera);
        }

        [Test]
        public void CameraWithoutTheLayer_Throws()
        {
            var camera = _camera.GetComponent<Camera>();
            camera.cullingMask = ~(1 << LayerMask.NameToLayer(Layer));
            var screenCanvas = new ScreenCanvasCamera(() => camera, Layer, PlaneDistance);

            Assert.Throws<InvalidOperationException>(() => screenCanvas.Attach(_canvas.GetComponent<Canvas>()));
        }

        [Test]
        public void PlaneOutsideTheClipPlanes_Throws()
        {
            var camera = _camera.GetComponent<Camera>();
            var screenCanvas = new ScreenCanvasCamera(() => camera, Layer, 2f);

            Assert.Throws<InvalidOperationException>(() => screenCanvas.Attach(_canvas.GetComponent<Canvas>()));
        }

        [Test]
        public void UnknownLayer_Throws()
        {
            var camera = _camera.GetComponent<Camera>();

            Assert.Throws<InvalidOperationException>(() => new ScreenCanvasCamera(() => camera, "No Such Layer", PlaneDistance));
        }

        [Test]
        public void Overlay_IsTheExplicitChoice()
        {
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            new ScreenCanvasOverlay().Attach(canvas);

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        }
    }
}
