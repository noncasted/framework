using UnityEngine;

namespace Global.Cameras
{
    public interface ICameraUtils
    {
        Vector3 ScreenToWorld(Vector3 screen);
    }

    public class CameraUtils : ICameraUtils
    {
        public CameraUtils(ICurrentCamera camera)
        {
            _camera = camera;
        }

        private readonly ICurrentCamera _camera;

        public Vector3 ScreenToWorld(Vector3 screen)
        {
            var camera = _camera.Current;

            if (camera == null)
                return Vector3.zero;

            // z of the screen point is the distance from the camera. A perspective camera maps every point at zero
            // distance to its own position, so the point is taken on the near plane. An orthographic camera keeps the
            // z of the caller: a point at any distance lies under the same screen point.
            if (camera.orthographic == false)
                screen.z = camera.nearClipPlane;

            return camera.ScreenToWorldPoint(screen);
        }
    }
}