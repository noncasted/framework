using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // The direct references between the assets of the project (AssetDatabase.GetDependencies, one level, read once per
    // path), without code and the built-in resources of the engine, which no bundle holds.
    public sealed class ContentAssetGraph
    {
        private static readonly string[] IgnoredExtensions = { ".cs", ".dll", ".asmdef", ".asmref" };

        private readonly Dictionary<string, string[]> _dependencies = new();

        // The asset and everything it references directly, in depth; a path for which stop is true is not entered.
        public IEnumerable<string> Walk(string start, Func<string, bool> stop)
        {
            var seen = new HashSet<string> { start };
            var stack = new Stack<string>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                var path = stack.Pop();
                yield return path;

                foreach (var dependency in Dependencies(path))
                {
                    if (seen.Add(dependency) && stop(dependency) == false)
                        stack.Push(dependency);
                }
            }
        }

        public static bool IsIgnored(string path)
        {
            if (path.StartsWith("Assets/", StringComparison.Ordinal) == false && path.StartsWith("Packages/", StringComparison.Ordinal) == false)
                return true;

            return IgnoredExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsScene(string path)
        {
            return path.EndsWith(".unity", StringComparison.Ordinal);
        }

        private string[] Dependencies(string path)
        {
            if (_dependencies.TryGetValue(path, out var dependencies))
                return dependencies;

            dependencies = AssetDatabase.GetDependencies(path, false).Where(dependency => dependency != path && IsIgnored(dependency) == false).ToArray();
            _dependencies.Add(path, dependencies);
            return dependencies;
        }
    }
}
