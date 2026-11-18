using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // The target of a reference in its asset: a component reference is in the project and in the bundles as its prefab.
    internal static class ContentTarget
    {
        // The type to load the asset of a target of T as.
        public static Type AssetType<T>() where T : Object
        {
            return IsComponent<T>() ? typeof(GameObject) : typeof(T);
        }

        // The target of T in a loaded asset (the component of a prefab for a component); null when the asset holds none.
        public static T Of<T>(Object asset) where T : Object
        {
            if (asset is GameObject prefab && IsComponent<T>())
                asset = prefab.GetComponent(typeof(T));

            return asset as T;
        }

        private static bool IsComponent<T>()
        {
            return typeof(Component).IsAssignableFrom(typeof(T));
        }
    }
}
