using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Tools/Content: record the load order of the content directories in Play Mode (play the menu and the levels with it
    // on, then commit ProjectSettings/ContentLoadOrder/<name>.txt, one per directory), clear them before a new recording.
    internal static class ContentLoadOrderMenu
    {
        private const string Record = "Tools/Content/Record Load Order";
        private const string Clear = "Tools/Content/Clear Load Order";

        [MenuItem(Record)]
        private static void Toggle()
        {
            ContentLoadOrder.IsRecording = ContentLoadOrder.IsRecording == false;
            Debug.Log($"[Content] Recording of the load order {(ContentLoadOrder.IsRecording ? "on" : "off")} (from the next Play Mode): " +
                      string.Join(", ", FrameworkSettings.ContentDirectories.Select(directory => ContentLoadOrder.PathOf(directory.Name))));
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
            foreach (var directory in FrameworkSettings.ContentDirectories)
            {
                ContentLoadOrder.Clear(directory.Name);
                Debug.Log($"[Content] Load order cleared: {ContentLoadOrder.PathOf(directory.Name)}");
            }
        }
    }
}
