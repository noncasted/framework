using Internal;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Global.UI
{
    // Whether a selectable is selected by the navigation (the gamepad), for a look of the selection on an element
    // that has no DesignButton (the bar of a menu slider). A selection made by a pointer press is not counted: a
    // click selects too, and UINavigation drops it in the other schemes a frame later.
    [DisallowMultipleComponent]
    public sealed class SelectionEvents : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private readonly ViewableProperty<bool> _isSelected = new(false);

        public IViewableProperty<bool> IsSelected => _isSelected;

        public static SelectionEvents Of(GameObject target)
        {
            if (target.TryGetComponent(out SelectionEvents events) == true)
                return events;

            return target.AddComponent<SelectionEvents>();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected.Set(eventData is not PointerEventData);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected.Set(false);
        }

        private void OnDisable()
        {
            _isSelected.Set(false);
        }
    }
}
