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
        public string ObjectPath;
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
            var queue = Load();
            queue.Requests.Add(request);
            SessionState.SetString(SessionKey, JsonUtility.ToJson(queue));
        }

        public static List<HierarchyBindingsRequest> Take()
        {
            var requests = Load().Requests;
            Clear();
            return requests;
        }

        public static bool IsEmpty()
        {
            return Load().Requests.Count == 0;
        }

        public static void Clear()
        {
            SessionState.EraseString(SessionKey);
        }

        private static HierarchyBindingsQueue Load()
        {
            var json = SessionState.GetString(SessionKey, string.Empty);

            if (string.IsNullOrEmpty(json))
                return new HierarchyBindingsQueue();

            try
            {
                return JsonUtility.FromJson<HierarchyBindingsQueue>(json) ?? new HierarchyBindingsQueue();
            }
            catch (Exception)
            {
                Clear();
                return new HierarchyBindingsQueue();
            }
        }
    }
}