using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    // The ids of the generated classes, in the form the generator writes them.
    [NoAutoStaticsCleanup]
    internal static class GeneratedScopeIds
    {
        // The entity loader asks for the id on every card, and it does not change for a method and view type:
        // the strings are built once (it used to be ~470 + ~300 bytes per load). The root key is the method handle:
        // the MethodInfo object can differ between delegates of the same method.
        private static readonly Dictionary<IntPtr, string> _rootIds = new Dictionary<IntPtr, string>();

        private static readonly Dictionary<(string, Type), string> _variantKeys =
            new Dictionary<(string, Type), string>();

        public static string VariantKey(string rootId, Type viewType)
        {
            if (_variantKeys.TryGetValue((rootId, viewType), out var key) == false)
            {
                key = rootId + "@" + TypeName(viewType);
                _variantKeys.Add((rootId, viewType), key);
            }

            return key;
        }

        // The type with dots and the method, for a local function - "+Name" after the outer method. A lambda cannot be
        // a root: the scope class cannot be found by it.
        public static string RootId(MethodInfo method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            var handle = method.MethodHandle.Value;

            if (_rootIds.TryGetValue(handle, out var id) == false)
            {
                id = BuildRootId(method);
                _rootIds.Add(handle, id);
            }

            return id;
        }

        private static string BuildRootId(MethodInfo method)
        {
            var type = method.DeclaringType;
            var name = method.Name;

            if (name.Length == 0 || name[0] != '<')
                return TypeName(type) + "." + name;

            var close = name.IndexOf('>');
            var marker = close < 0 ? -1 : name.IndexOf("g__", close, StringComparison.Ordinal);
            var bar = marker < 0 ? -1 : name.IndexOf('|', marker);

            if (close < 0 || marker != close + 1 || bar < 0)
            {
                throw new ArgumentException(
                    "Scope root must be a method group or a local function, not a lambda: " + name,
                    nameof(method));
            }

            var outer = name.Substring(1, close - 1);
            var local = name.Substring(marker + 3, bar - marker - 3);

            while (type != null && type.Name.Length > 0 && type.Name[0] == '<')
                type = type.DeclaringType;

            return TypeName(type) + "." + outer + "+" + local;
        }

        private static string TypeName(Type type)
        {
            if (type == null)
                return string.Empty;

            return (type.FullName ?? type.Name).Replace('+', '.');
        }
    }
}
