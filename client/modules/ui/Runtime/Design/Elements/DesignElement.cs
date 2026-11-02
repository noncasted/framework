using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignElement : MonoBehaviour, IDesignElement
    {
        private readonly ViewableProperty<DesignElementState> _state = new(DesignElementState.Idle);

        public IViewableProperty<DesignElementState> State => _state;

        public IReadOnlyLifetime Lifetime => this.GetObjectLifetime();

        public void SetState(DesignElementState state)
        {
            _state.Set(state);
        }
    }
}