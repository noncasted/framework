using UnityEditor;
using UnityEngine;

namespace Internal
{
    public static class HierarchyBindingsMenu
    {
        private const string HierarchyPath = "GameObject/Create Object Bindings";
        private const string ToolsPath = FrameworkMenu.Root + "Create Object Bindings";

        // The validator must be parameterless, otherwise Unity does not register the item at all.
        // We work from Selection, not MenuCommand: this way the item is invoked once,
        // not once per selected object.
        [MenuItem(HierarchyPath, false, 30)]
        private static void OpenFromHierarchy()
        {
            Open();
        }

        [MenuItem(HierarchyPath, true)]
        private static bool ValidateHierarchy()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(ToolsPath, false, FrameworkMenu.Priority)]
        private static void OpenFromTools()
        {
            Open();
        }

        [MenuItem(ToolsPath, true)]
        private static bool ValidateTools()
        {
            return Selection.activeGameObject != null;
        }

        private static void Open()
        {
            var target = Selection.activeGameObject;

            if (target == null)
            {
                Debug.LogWarning("[HierarchyBindingsGenerator] Select a GameObject first.");
                return;
            }

            HierarchyBindingsWindow.Open(target);
        }
    }
}