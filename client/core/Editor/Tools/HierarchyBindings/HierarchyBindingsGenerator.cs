using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Internal
{
    // Generation runs in two phases: first we write the class, then, after compilation, attach it
    // to the object and fill the references. The request lives in SessionState between the phases
    // because the domain reloads in between.
    public static class HierarchyBindingsGenerator
    {
        public const string TypeNameSuffix = "Bindings";

        private const string LogTag = "HierarchyBindingsGenerator";

        public static string ToTypeName(string bindingsName)
        {
            var identifier = HierarchyBindingsNaming.ToIdentifier(bindingsName);

            if (string.IsNullOrEmpty(identifier))
                return string.Empty;

            if (identifier.EndsWith(TypeNameSuffix, StringComparison.Ordinal))
                return identifier;

            return identifier + TypeNameSuffix;
        }

        [InitializeOnLoadMethod]
        private static void InstallRegenerateHandler()
        {
            ObjectBindings.RegenerateHandler = Regenerate;
        }

        public static void Regenerate(ObjectBindings bindings)
        {
            var type = ResolveGeneratedType(bindings);

            if (type == null)
            {
                Debug.LogError($"[{LogTag}] Failed to resolve the generated type.", bindings);
                return;
            }

            Generate(
                    bindings.gameObject,
                    type.Name,
                    type.Namespace ?? string.Empty,
                    bindings.IsSceneService,
                    bindings.IsEntityComponent
                );
        }

        // The user class inherits the generated one, so from the component on the object we
        // have to walk up to the type directly under ObjectBindings - that is the generated one.
        public static Type ResolveGeneratedType(Component bindings)
        {
            if (bindings == null)
                return null;

            var type = bindings.GetType();

            while (type != null && type.BaseType != typeof(ObjectBindings))
                type = type.BaseType;

            return type;
        }

        public static void Generate(
            GameObject root,
            string bindingsName,
            string namespaceName,
            bool isSceneService,
            bool isEntityComponent)
        {
            if (Prepare(root, bindingsName, namespaceName, isSceneService, isEntityComponent, out var changed) == false)
                return;

            Flush(changed);
        }

        // Code is written separately from triggering compilation: batch regeneration writes all the
        // classes and only then calls Refresh once - otherwise the domain would reload mid-traversal.
        // bindUnchanged = false skips the binding phase when the generated code did not change (Regenerate All:
        // the same code means the same structure, and binding would re-save every prefab).
        public static bool Prepare(
            GameObject root,
            string bindingsName,
            string namespaceName,
            bool isSceneService,
            bool isEntityComponent,
            out bool changed,
            bool bindUnchanged = true)
        {
            changed = false;

            var typeName = ToTypeName(bindingsName);

            if (string.IsNullOrEmpty(typeName))
            {
                Debug.LogError($"[{LogTag}] '{bindingsName}' is not a C# identifier.");
                return false;
            }

            var existing = root.GetComponent<ObjectBindings>();

            // A prefab instance carries the bindings of its asset. Generating from the instance would name the root
            // after the instance and write the hash and references as overrides of the scene, not into the asset.
            if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing) != null)
            {
                Debug.LogError(
                    $"[{LogTag}] '{root.name}' carries bindings of prefab " +
                    $"{AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(existing))}. " +
                    "Regenerate them in that prefab.", root);
                return false;
            }

            if (HierarchyBindingsTarget.TryDescribe(root, out var assetPath, out var objectPath, out var isPrefab,
                    out var describeError) ==
                false)
            {
                Debug.LogError($"[{LogTag}] {describeError}", root);
                return false;
            }

            var errors = new List<string>();
            var node = HierarchyBindingsScanner.Scan(root, typeName, errors);

            if (node == null)
            {
                Report(errors, root);
                return false;
            }

            var folder = HierarchyBindingsPaths.ResolveGeneratedFolder(root, assetPath);
            var filePath = $"{folder}/{typeName}.g.cs";
            var structureHash = HierarchyBindingsStructure.ComputeHash(node);

            var code = HierarchyBindingsCodeGenerator.Build(
                    node,
                    namespaceName,
                    structureHash,
                    isSceneService,
                    isEntityComponent
                );

            changed = HasChanged(filePath, code);

            // Same code but a stale hash on the object: an earlier binding failed or never ran (the object was
            // saved with old references). Bind again, otherwise the object never catches up with its code.
            var isBoundStale = existing == null || existing.StructureHash != structureHash;

            if (changed || bindUnchanged || isBoundStale)
            {
                HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest
                {
                    AssetPath = assetPath,
                    ObjectPath = objectPath,
                    TypeName = typeName,
                    Namespace = namespaceName,
                    IsPrefabAsset = isPrefab,
                    IsSceneService = isSceneService,
                    IsEntityComponent = isEntityComponent,
                    StructureHash = structureHash
                });
            }

            CatalogPaths.EnsureFolder(folder);
            GeneratedFile.WriteIfChanged(LogTag, filePath, code);
            return true;
        }

        // Without code changes there is no compilation, so DidReloadScripts will not fire either,
        // hence we bind right away.
        public static void Flush(bool changed)
        {
            if (changed)
                AssetDatabase.Refresh();
            else
                EditorApplication.delayCall += ProcessPending;
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (HierarchyBindingsQueue.IsEmpty())
                return;

            EditorApplication.delayCall += ProcessPending;
        }

        private static void ProcessPending()
        {
            var requests = HierarchyBindingsQueue.Take();

            if (requests.Count == 0)
                return;

            var bound = 0;

            foreach (var request in requests)
            {
                if (Process(request))
                    bound++;
            }

            if (bound == 0)
                return;

            AssetDatabase.SaveAssets();
            Debug.Log($"[{LogTag}] Bound {bound} of {requests.Count} bindings.");
        }

        private static bool Process(HierarchyBindingsRequest request)
        {
            var type = FindType(request);

            if (type == null)
            {
                Debug.LogError(
                    $"[{LogTag}] Generated type '{request.TypeName}' not found. Fix compilation errors and generate again.");
                return false;
            }

            using var target = HierarchyBindingsTarget.Resolve(request, out var resolveError);

            if (target == null)
            {
                Debug.LogError($"[{LogTag}] {resolveError}");
                return false;
            }

            var errors = new List<string>();

            if (HierarchyBindingsBinder.Bind(target.Root, type, request, errors) == false)
            {
                Report(errors, target.Root);
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

        private static bool HasChanged(string assetPath, string content)
        {
            try
            {
                var fullPath = CatalogPaths.ToFullPath(assetPath);
                return File.Exists(fullPath) == false || File.ReadAllText(fullPath) != content;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static void Report(List<string> errors, UnityEngine.Object context)
        {
            foreach (var error in errors)
                Debug.LogError($"[{LogTag}] {error}", context);
        }
    }
}