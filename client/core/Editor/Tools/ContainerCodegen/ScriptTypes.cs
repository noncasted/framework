using System;
using System.Collections.Generic;
using UnityEditor;

namespace Internal
{
    // Script GUID -> full class name. Unity binds a script to the class named after the file, not to the
    // first class in it, so MonoScript.GetClass() is the source of truth in a fresh domain. The source text is
    // used for a script that is not in the compiled assemblies yet and while the last compile failed (the loaded
    // domain still has the old names, see ScriptTypeNames).
    internal sealed class ScriptTypes
    {
        private readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string guid, out string type)
        {
            if (_cache.TryGetValue(guid, out type) == false)
            {
                type = Find(guid);
                _cache[guid] = type;
            }

            return string.IsNullOrEmpty(type) == false;
        }

        private static string Find(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == false)
                return "";

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var compiled = script != null ? script.GetClass() : null;

            if (compiled != null && ScriptTypeNames.IsDomainStale == false)
                return compiled.FullName;

            if (ScriptTypeNames.TryReadSource(path, out var fullName, out _))
                return fullName;

            return compiled != null ? compiled.FullName : "";
        }
    }
}
