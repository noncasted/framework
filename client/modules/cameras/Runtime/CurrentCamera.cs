using System;
using Internal;
using UnityEngine;

namespace Global.Cameras
{
    public interface ICurrentCamera
    {
        // The camera the game renders with now; null until the game sets one and after the lifetime it was set with
        // ends (the scene of the camera is unloaded).
        Camera Current { get; }

        // The same camera as a reactive value: a listener gets every change of the camera (Current reads its value).
        IViewableProperty<Camera> Value { get; }

        // The camera is current until a later one is set or the lifetime ends (then it is null, and the listeners of
        // Value are told): a camera of an unloaded scene did not stay current as a destroyed object.
        void SetCamera(IReadOnlyLifetime lifetime, Camera current);

        // The output resolution of the game: the screen and the render texture of the current camera, when it renders
        // into one. Kept and applied again to every camera set later.
        void SetOutputResolution(int width, int height, FullScreenMode mode);
    }

    public class CurrentCamera : ICurrentCamera
    {
        public CurrentCamera()
        {
            _clearCamera = ClearCamera;
        }

        private readonly ViewableProperty<Camera> _value = new();
        private readonly Action _clearCamera;

        private IReadOnlyLifetime _cameraLifetime;

        private int _outputWidth;
        private int _outputHeight;
        private FullScreenMode _outputMode;
        private bool _hasOutput;

        public Camera Current => _value.Value;
        public IViewableProperty<Camera> Value => _value;

        public void SetCamera(IReadOnlyLifetime lifetime, Camera current)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current), "[Cameras] A current camera that does not exist");

            // The listener of the previous camera goes: a long lifetime (the global camera, set again after every
            // scene) kept one per call.
            _cameraLifetime?.RemoveListener(_clearCamera);

            _value.Set(current);
            ResizeTarget(current);

            _cameraLifetime = lifetime;
            lifetime.Listen(_clearCamera);
        }

        public void SetOutputResolution(int width, int height, FullScreenMode mode)
        {
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Resolution {width}x{height} is invalid.");

            _outputWidth = width;
            _outputHeight = height;
            _outputMode = mode;
            _hasOutput = true;

            if (_value.Value != null)
                ResizeTarget(_value.Value);

            // The screen is set here only, when the game asks for it: set again on every camera, it overrode the size
            // the browser or the OS gave the window since (a resized canvas on WebGL, a rotated phone).
            if (Screen.width == _outputWidth &&
                Screen.height == _outputHeight &&
                Screen.fullScreenMode == _outputMode)
                return;

            Screen.SetResolution(_outputWidth, _outputHeight, _outputMode);
        }

        private void ClearCamera()
        {
            _cameraLifetime = null;
            _value.Set(null);
        }

        // Without a resolution set by the game the render textures stay as they are.
        private void ResizeTarget(Camera camera)
        {
            if (_hasOutput == false || camera.targetTexture == null)
                return;

            Resize(camera.targetTexture);
        }

        private void Resize(RenderTexture target)
        {
            if (target.width == _outputWidth && target.height == _outputHeight)
                return;

            if (target.IsCreated())
                target.Release();

            target.width = _outputWidth;
            target.height = _outputHeight;

            if (target.Create() == false)
                throw new InvalidOperationException($"Camera output {_outputWidth}x{_outputHeight} was not created.");
        }
    }
}