using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // A binding request survives the domain reload: first we write the code, after compilation
    // we return to the same object and fill the references.
    [Serializable]
    internal sealed class HierarchyBindingsRequest
    {
        public string AssetPath;

        // Sibling indices from the root of the asset ("1/0"; for a scene the first one is the index among the root
        // objects of the scene; empty - the root itself). Names are not unique and may contain '/'.
        public string ObjectPath;

        // The names of the same path, for messages only.
        public string ObjectNames;

        public string TypeName;
        public string Namespace;
        public bool IsPrefabAsset;
        public bool IsSceneService;
        public bool IsEntityComponent;

        // Hash of the structure the code was generated from. The binding phase scans the object again and must get
        // the same structure.
        public string StructureHash;
    }

    // There can be many requests: scene regeneration writes all classes at once, and the compilation after
    // that is a single one for all, so the queue is processed entirely in one pass.
    [Serializable]
    internal sealed class HierarchyBindingsQueue
    {
        private const string SessionKey = "Internal.HierarchyBindings.PendingRequests";

        public List<HierarchyBindingsRequest> Requests = new();

        public static void Enqueue(HierarchyBindingsRequest request)
        {
            var queue = Read();
            queue.Requests.Add(request);
            SessionState.SetString(SessionKey, JsonUtility.ToJson(queue));
        }

        public static List<HierarchyBindingsRequest> Load()
        {
            return Read().Requests;
        }

        public static bool IsEmpty()
        {
            return Read().Requests.Count == 0;
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