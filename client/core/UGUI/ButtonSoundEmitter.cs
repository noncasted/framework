using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Internal
{
    [DisallowMultipleComponent]
    public sealed class ButtonSoundEmitter : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClicked);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button.IsInteractable() == true)
                ButtonSounds.Hovered?.Invoke();
        }

        // The selection by the gamepad sounds as the hover; a click selects too, that one is not a hover.
        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is PointerEventData)
                return;

            if (_button.IsInteractable() == true)
                ButtonSounds.Hovered?.Invoke();
        }

        private static void OnClicked()
        {
            ButtonSounds.Clicked?.Invoke();
        }
    }
}
