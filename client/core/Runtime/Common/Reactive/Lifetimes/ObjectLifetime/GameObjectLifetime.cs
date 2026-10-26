using UnityEngine;

namespace Internal
{
    // The lifetime of an active period of the object: it ends when the object is disabled or destroyed. Asked while
    // the object is hidden (a screen built before it is shown, or one hidden after it was shown) it is the lifetime
    // of the next active period: it lives while the object stays hidden and ends with the disable after the showing.
    [DisallowMultipleComponent]
    public class GameObjectLifetime : MonoBehaviour, IGameObjectLifetime
    {
        private ILifetime _lifetime;

        private void OnDisable()
        {
            if (_lifetime is { IsTerminated: false })
                _lifetime.Terminate();
        }

        // An object destroyed while hidden gets no OnDisable. Unity sends OnDestroy only to an object that has been
        // active once: the lifetime of an object destroyed before it was ever shown does not end.
        private void OnDestroy()
        {
            if (_lifetime is { IsTerminated: false })
                _lifetime.Terminate();
        }

        public IReadOnlyLifetime GetValidLifetime()
        {
            if (_lifetime is { IsTerminated: false })
                return _lifetime;

            _lifetime = new Lifetime();

            return _lifetime;
        }

        public void ForceTerminate()
        {
            if (_lifetime is { IsTerminated: false })
                _lifetime.Terminate();

            _lifetime = null;
        }
    }
}