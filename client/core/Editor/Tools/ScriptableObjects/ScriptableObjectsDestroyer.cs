using System;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Internal
{
    public class ScriptableObjectsDestroyer
    {
        private const string MenuPath = "Assets/Destroy Nested Objects";

        // For nested objects only: without it the first item of the context menu of any asset destroys that asset.
        [MenuItem(MenuPath, true)]
        private static bool CanDestroyObject()
        {
            return Selection.objects.Length > 0 && Selection.objects.All(AssetDatabase.IsSubAsset);
        }

        [MenuItem(MenuPath, priority = -1000)]
        private static void DestroyObject()
        {
            if (CanDestroyObject() == false)
                throw new InvalidOperationException("[ScriptableObjectsDestroyer] The selection has more than nested objects");

            foreach (var target in Selection.objects)
            {
                Object.DestroyImmediate(target, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}
