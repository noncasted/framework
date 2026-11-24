using System;
using System.Reflection;

namespace Internal
{
    // RuntimeInitializeOnLoadMethod is not called in edit mode: a test that builds a scope of a generated root registers
    // the generated class of the root here first.
    public static class GeneratedRoots
    {
        // Returns the root id of the method.
        public static string Register(MethodInfo root)
        {
            var rootId = GeneratedScopes.RootId(root);

            if (GeneratedScopes.IsRegistered(rootId) == true)
                return rootId;

            var name = root.DeclaringType.Name + root.Name + "Container";
            var type = root.DeclaringType.Assembly.GetType(root.DeclaringType.Namespace + "." + name);

            if (type == null)
                throw new InvalidOperationException("No generated class " + name + " for " + rootId);

            var method = type.GetMethod("RegisterGenerated", BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, null);
            return rootId;
        }
    }
}
