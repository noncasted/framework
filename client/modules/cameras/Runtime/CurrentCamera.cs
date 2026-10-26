using System;
using Internal;
using UnityEngine;

namespace Global.Cameras
{
    public interface ICurrentCamera
    {
        // The camera the game renders with now; null until the game sets one.
        Camera Current { get; }

        // The same camera as a reactive value: a listener gets every change of the camera (Current reads its value).
        IViewableProperty<Camera> Value { get; }

        void SetCamera(Camera current);

        // The output resolution of the game: the screen and the render texture of the current camera, when it renders
        // into one. Kept and applied again to every camera set later.
        void SetOutputResolution(int width, int height, FullScreenMode mode);
    }

    public class CurrentCamera : ICurrentCamera
    {
        private readonly ViewableProperty<Camera> _value = new();

        private int _outputWidth;
        private int _outputHeight;
        private FullScreenMode _outputMode;
        private bool _hasOutput;

        public Camera Current => _value.Value;
        public IViewableProperty<Camera> Value => _value;

        public void SetCamera(Camera current)
        {
            _value.Set(current);
            ApplyOutput(current);
        }

        public void SetOutputResolution(int width, int height, FullScreenMode mode)
        {
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Resolution {width}x{height} is invalid.");

            _outputWidth = width;
            _outputHeight = height;
            _outputMode = mode;
            _hasOutput = true;
            ApplyOutput(_value.Value);
        }

        // Without a resolution set by the game the screen and the render textures stay as they are.
        private void ApplyOutput(Camera camera)
        {
            if (_hasOutput == false)
                return;

            if (camera != null && camera.targetTexture != null)
                Resize(camera.targetTexture);

            if (Screen.width == _outputWidth &&
                Screen.height == _outputHeight &&
                Screen.fullScreenMode == _outputMode)
                return;

            Screen.SetResolution(_outputWidth, _outputHeight, _outputMode);
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