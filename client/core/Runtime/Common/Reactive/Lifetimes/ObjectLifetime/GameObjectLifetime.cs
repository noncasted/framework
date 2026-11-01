using Cysharp.Threading.Tasks;
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
        private bool _isAwake;

        private void Awake()
        {
            _isAwake = true;
        }

        private void OnDisable()
        {
            if (_lifetime is { IsTerminated: false })
                _lifetime.Terminate();
        }

        // An object destroyed while hidden gets no OnDisable. Unity sends OnDestroy only to an object that has been
        // active once: WatchNeverShown ends the lifetime of an object destroyed before it was ever shown.
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

            if (_isAwake == false)
                WatchNeverShown(_lifetime).Forget();

            return _lifetime;
        }

        // No message tells that an object that has never been active is destroyed, so this one case is looked at once a
        // frame, until the object wakes up (OnDisable and OnDestroy take over) or the lifetime ends.
        private async UniTaskVoid WatchNeverShown(ILifetime lifetime)
        {
            while (_isAwake == false && lifetime.IsTerminated == false)
            {
                await UniTask.Yield();

                if (this == null)
                {
                    lifetime.Terminate();
                    return;
                }
            }
        }

        public void ForceTerminate()
        {
            if (_lifetime is { IsTerminated: false })
                _lifetime.Terminate();

            _lifetime = null;
        }
    }
}