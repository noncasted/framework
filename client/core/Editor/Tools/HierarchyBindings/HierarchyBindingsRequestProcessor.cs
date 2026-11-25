using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The second phase of one request, after the reload: the class is attached to the object and its references filled.
    internal static class HierarchyBindingsRequestProcessor
    {
        private static readonly ILog _log = Logs.Of(typeof(HierarchyBindingsRequestProcessor));

        public static bool Process(HierarchyBindingsRequest request)
        {
            var type = FindType(request);

            if (type == null)
            {
                _log.Error("Generated type '{Type}' not found. Fix compilation errors and generate again.", request.TypeName);
                return false;
            }

            using var target = HierarchyBindingsTarget.Resolve(request, out var resolveError);

            if (target == null)
            {
                _log.Error("{Error}", resolveError);
                return false;
            }

            var errors = new List<string>();

            if (HierarchyBindingsBinder.Bind(target.Root, type, request, errors) == false)
            {
                foreach (var error in errors)
                    Debug.LogError($"[HierarchyBindingsGenerator] {error}", target.Root);

                return false;
            }

            target.Commit();
            return true;
        }

        private static Type FindType(HierarchyBindingsRequest request)
        {
            var fullName = string.IsNullOrEmpty(request.Namespace)
                ? request.TypeName
                : request.Namespace + "." + request.TypeName;

            foreach (var candidate in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (string.Equals(candidate.FullName, fullName, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }
    }
}
