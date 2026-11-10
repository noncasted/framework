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

        // The last line of a batch: an error when its validation found something.
        public static void ReportResult(string message, int validationErrors)
        {
            if (validationErrors > 0)
                Debug.LogError($"[{Tag}] {message}");
            else
                Debug.Log($"[{Tag}] {message}");
        }
    }
}
