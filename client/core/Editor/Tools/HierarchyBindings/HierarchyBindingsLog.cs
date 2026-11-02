using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The tag of the messages of the generator and of its batch.
    internal static class HierarchyBindingsLog
    {
        public const string Tag = "HierarchyBindingsGenerator";

        public static void Report(List<string> errors, UnityEngine.Object context)
        {
            foreach (var error in errors)
                Debug.LogError($"[{Tag}] {error}", context);
        }
    }
}
