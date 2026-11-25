using Internal;
using UnityEngine;

namespace Global.UI
{
    public static class UIStateHandleExtensions
    {
        public static void AttachGameObject(this IUIStateHandle handle, GameObject target)
        {
            handle.IsVisible.View(handle.InnerLifetime, target.SetActive);
        }
    }
}
