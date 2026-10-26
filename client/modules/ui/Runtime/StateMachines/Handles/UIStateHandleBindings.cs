using System;
using Internal;
using UnityEngine;

namespace Global.UI
{
    // Actions bound to the visibility of a state: shown - the state is visible (entered, the state stacked over it
    // exited), hidden - it is not visible any more for any reason (a state stacked over it, Exit, the end of its
    // owner or parent lifetime).
    public static class UIStateHandleBindings
    {
        // onHidden is optional: null - nothing is done on hide.
        public static void BindVisible(
            this IUIStateHandle handle,
            Action onVisible,
            Action onHidden)
        {
            handle.BindVisible(visibleLifetime => {
                onVisible.Invoke();

                if (onHidden != null)
                    visibleLifetime.Listen(onHidden);
            });
        }

        // onHidden is optional: null - nothing is done on hide.
        public static void BindVisible(
            this IUIStateHandle handle,
            Action<IReadOnlyLifetime> onVisible,
            Action onHidden)
        {
            handle.BindVisible(visibleLifetime => {
                onVisible.Invoke(visibleLifetime);

                if (onHidden != null)
                    visibleLifetime.Listen(onHidden);
            });
        }

        // The lifetime given to onVisible lives while the state is visible.
        public static void BindVisible(
            this IUIStateHandle handle,
            Action<IReadOnlyLifetime> onVisible)
        {
            handle.IsVisible.ViewTrue(handle.InnerLifetime, onVisible);
        }

        // The object is active while the state is visible. Unlike AttachGameObject, it is deactivated also when the
        // handle ends without Exit (its owner or parent lifetime ended), so it must outlive the handle.
        public static void BindGameObject(this IUIStateHandle handle, GameObject target)
        {
            handle.BindVisible(
                () => target.SetActive(true),
                () => target.SetActive(false));
        }
    }
}
