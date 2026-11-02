using Internal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Global.UI
{
    public class DesignButton :
        MonoBehaviour,
        IDesignButton,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [SerializeField] private DesignElement _element;
        [SerializeField] private Button _button;

        private readonly ViewableDelegate _clicked = new();
        private bool _isLocked;
        private DesignElementState _state;

        // The look is the pointer (inside, pressed) or the selection by the gamepad, which looks as the
        // hover. A selection made by a pointer press is not counted (a click selects too).
        private bool _isPointerInside;
        private bool _isPointerDown;
        private bool _isSelected;

        public DesignElement Element => _element;
        public IViewableDelegate Clicked => _clicked;

        private void OnEnable()
        {
            ButtonSounds.Attach(_button);
            _isPointerInside = false;
            _isPointerDown = false;
            _isSelected = false;
            _state = DesignElementState.Idle;
            _element.SetState(DesignElementState.Idle);
            _button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClicked);
        }

        public void Lock()
        {
            _isLocked = true;
        }

        public void Unlock()
        {
            _isLocked = false;
            _element.SetState(_state);
        }

        public void OnClicked()
        {
            if (_isLocked == true)
                return;

            _clicked.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            Refresh();
        }

        // The pointer leaving ends a press too: the look was Idle after an exit before the gamepad selection was added.
        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            _isPointerDown = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPointerDown = true;
            Refresh();
        }

        // The input module sends the rest: a pointer released outside has exited already, a touch exits right after.
        public void OnPointerUp(PointerEventData eventData)
        {
            _isPointerDown = false;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected = eventData is not PointerEventData;
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            Refresh();
        }

        private void Refresh()
        {
            if (_isPointerDown == true)
                SetState(DesignElementState.Pressed);
            else if (_isPointerInside == true || _isSelected == true)
                SetState(DesignElementState.Hovered);
            else
                SetState(DesignElementState.Idle);
        }

        private void SetState(DesignElementState state)
        {
            _state = state;

            if (_isLocked == true)
                return;

            _element.SetState(state);
        }

        private void OnValidate()
        {
            if (_element == null)
                _element = GetComponent<DesignElement>();

            if (_element == null)
                _element = gameObject.AddComponent<DesignElement>();

            if (_button == null)
                _button = GetComponent<Button>();
        }
    }
}