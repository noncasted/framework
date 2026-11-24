using System;
using UnityEditor;

namespace Unity.Web.Stripping.Editor
{
    // Need to use reflection for BuildProfileWindow as its an internal class
    static class BuildProfileWindowHelper
    {
        // WeakReference avoids pinning the assembly, which would cause ALC leaks under CoreCLR.
        static WeakReference<Type> s_TypeRef;

        internal static Type Type
        {
            get
            {
                if (s_TypeRef != null && s_TypeRef.TryGetTarget(out var cachedType))
                    return cachedType;

                var type = AssemblyHelper.FindLoadedAssembly("UnityEditor.BuildProfileModule")
                    ?.GetType("UnityEditor.Build.Profile.BuildProfileWindow");

                if (type != null)
                    s_TypeRef = new WeakReference<Type>(type);

                return type;
            }
        }

        public static EditorWindow GetWindow()
        {
            return EditorWindow.GetWindow(Type);
        }
    }
}
