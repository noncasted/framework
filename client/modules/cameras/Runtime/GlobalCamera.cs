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
    public class GlobalCamera : MonoBehaviour, IGlobalCamera, IScopeBaseSetup
    {
        private ICurrentCamera _currentCamera;
        private IReadOnlyLifetime _lifetime;

        public Camera Camera { get; private set; }

        [Inject]
        public void Construct(ICurrentCamera currentCamera)
        {
            _currentCamera = currentCamera;
        }

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            Camera = GetComponent<Camera>();
            _lifetime = lifetime;
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