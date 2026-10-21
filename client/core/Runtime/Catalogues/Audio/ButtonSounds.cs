using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Internal
{
    // Internal cannot see the Global audio player, so buttons report clicks and hovers through hooks
    // that the player sets. The emitter is attached to the button in ListenClick, the common entry point of all buttons.
    [AutoStaticsCleanup]
    public static partial class ButtonSounds
    {
        public static Action Clicked { get; set; }
        public static Action Hovered { get; set; }

        public static void Attach(Button button)
        {
            if (button.TryGetComponent(out ButtonSoundEmitter _) == false)
                button.gameObject.AddComponent<ButtonSoundEmitter>();
        }
    }

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
