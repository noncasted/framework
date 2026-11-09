using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Tools/Content: record the load order of the content directory in Play Mode (play the menu and the levels with it
    // on, then commit ProjectSettings/ContentLoadOrder/<name>.txt), clear it before a new recording.
    internal static class ContentLoadOrderMenu
    {
        private const string Record = "Tools/Content/Record Load Order";
        private const string Clear = "Tools/Content/Clear Load Order";

        [MenuItem(Record)]
        private static void Toggle()
        {
            ContentLoadOrder.IsRecording = ContentLoadOrder.IsRecording == false;
            Debug.Log($"[Content] Recording of the load order {(ContentLoadOrder.IsRecording ? "on" : "off")} (from the next Play Mode): " +
                      ContentLoadOrder.PathOf(FrameworkSettings.ContentDirectory.Name));
        }

        [MenuItem(Record, true)]
        private static bool ToggleState()
        {
            Menu.SetChecked(Record, ContentLoadOrder.IsRecording);
            return true;
        }

        [MenuItem(Clear)]
        private static void ClearOrder()
        {
            var name = FrameworkSettings.ContentDirectory.Name;
            ContentLoadOrder.Clear(name);
            Debug.Log($"[Content] Load order cleared: {ContentLoadOrder.PathOf(name)}");
        }
    }
}
