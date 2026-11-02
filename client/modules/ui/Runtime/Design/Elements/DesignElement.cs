using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignElement : MonoBehaviour, IDesignElement
    {
        private readonly ViewableProperty<DesignElementState> _state = new(DesignElementState.Idle);

        private IReadOnlyLifetime _lifetime;

        public IViewableProperty<DesignElementState> State => _state;

        public IReadOnlyLifetime Lifetime
        {
            get
            {
                if (_lifetime == null || _lifetime.IsTerminated == true)
                    _lifetime = this.GetObjectLifetime();

                return _lifetime;
            }
        }

        public void SetState(DesignElementState state)
        {
            _state.Set(state);
        }

        private void OnEnable()
        {
            _lifetime = this.GetObjectLifetime();
        }
    }
}