using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // There can be many requests: scene regeneration writes all classes at once, and the compilation after
    // that is a single one for all, so the queue is processed entirely in one pass.
    [Serializable]
    internal sealed class HierarchyBindingsQueue
    {
        private const string SessionKey = "Internal.HierarchyBindings.PendingRequests";

        [SerializeField] private List<HierarchyBindingsRequest> _requests = new();

        public static void Enqueue(HierarchyBindingsRequest request)
        {
            var queue = Read();
            queue._requests.Add(request);
            SessionState.SetString(SessionKey, JsonUtility.ToJson(queue));
        }

        public static List<HierarchyBindingsRequest> Load()
        {
            return Read()._requests;
        }

        public static bool IsEmpty()
        {
            return Read()._requests.Count == 0;
        }

        // Without parsing: the menu asks it on every repaint, and a queue that cannot be read must still be resettable.
        public static bool HasData()
        {
            return string.IsNullOrEmpty(SessionState.GetString(SessionKey, string.Empty)) == false;
        }

        public static void Clear()
        {
            SessionState.EraseString(SessionKey);
        }

        // A queue that cannot be read is not an empty one: its requests would be lost without a word.
        private static HierarchyBindingsQueue Read()
        {
            var json = SessionState.GetString(SessionKey, string.Empty);

            if (string.IsNullOrEmpty(json))
                return new HierarchyBindingsQueue();

            HierarchyBindingsQueue queue;

            try
            {
                queue = JsonUtility.FromJson<HierarchyBindingsQueue>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "[HierarchyBindingsGenerator] The queue of pending bindings is unreadable. Reset it with " +
                    "Cancel Prefab Batch and generate again.", exception);
            }

            if (queue == null)
                throw new InvalidOperationException(
                    "[HierarchyBindingsGenerator] The queue of pending bindings is unreadable. Reset it with " +
                    "Cancel Prefab Batch and generate again.");

            return queue;
        }
    }
}
