using Internal;
using UnityEngine;

namespace Global.Cameras
{
    public interface IGlobalCamera
    {
        Camera Camera { get; }
        void Enable();
        void Disable();
    }

    [DisallowMultipleComponent]
    public class GlobalCamera : MonoBehaviour, IGlobalCamera
    {
        private ICurrentCamera _currentCamera;
        private IReadOnlyLifetime _lifetime;

        public Camera Camera { get; private set; }

        // Ready once injected: a service may enable it from its own setup.
        [Inject]
        public void Construct(ICurrentCamera currentCamera, IReadOnlyLifetime lifetime)
        {
            _currentCamera = currentCamera;
            _lifetime = lifetime;
            Camera = GetComponent<Camera>();
        }

        public void Enable()
        {
            gameObject.SetActive(true);
            _currentCamera.SetCamera(_lifetime, Camera);
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }
    }
}