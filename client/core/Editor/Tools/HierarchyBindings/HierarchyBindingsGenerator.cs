using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
        private const string AwaitingReloadKey = "Internal.HierarchyBindings.AwaitingReload";

        // The namespace line HierarchyBindingsCodeGenerator writes; a class without a namespace has none.
        private static readonly Regex DeclaredNamespace = new(@"^namespace (\S+) \{", RegexOptions.Multiline);

        public static string ToTypeName(string bindingsName)
        {
            var identifier = HierarchyBindingsNaming.ToIdentifier(bindingsName);

            if (string.IsNullOrEmpty(identifier))
                return string.Empty;

            if (identifier.EndsWith(TypeNameSuffix, StringComparison.Ordinal))
                return identifier;

            return identifier + TypeNameSuffix;
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

            if (HierarchyBindingsTarget.TryDescribe(root, out var assetPath, out var objectPath, out var isPrefab,
                    out var describeError) ==
                false)
            {
                Debug.LogError($"[{LogTag}] {describeError}", root);
                return false;
            }

            var errors = new List<string>();
            string code = null;
            string structureHash = null;
            string boundHash = null;

            using (var target = HierarchyBindingsTarget.OpenForScan(root, assetPath, objectPath, isPrefab,
                       out var openError))
            {
                if (target == null)
                {
                    Debug.LogError($"[{LogTag}] {openError}", root);
                    return false;
                }

                // The node holds references to objects of the loaded contents, so the code is built
                // before they are unloaded.
                var node = Scan(target.Root, typeName, namespaceName, assetPath, isPrefab, isSceneService,
                    isEntityComponent, errors);

                if (node != null)
                {
                    structureHash = HierarchyBindingsStructure.ComputeHash(node);
                    boundHash = FindBindings(target.Root, typeName, namespaceName)?.StructureHash;
                    code = HierarchyBindingsCodeGenerator.Build(
                            node,
                            namespaceName,
                            structureHash,
                            isSceneService,
                            isEntityComponent
                        );
                }
            }

            if (code == null)
            {
                Report(errors, root);
                return false;
            }

            var folder = HierarchyBindingsPaths.ResolveGeneratedFolder(root, assetPath);
            var filePath = ToFilePath(folder, typeName);
            var fileConflict = FindFileConflict(filePath, typeName, namespaceName);

            if (fileConflict != null)
            {
                Debug.LogError($"[{LogTag}] {fileConflict}", root);
                return false;
            }

            // The code is written before the request is queued: a failed write throws and leaves no request waiting
            // for a reload that never comes. Changed means written, so a reload follows.
            CatalogPaths.EnsureFolder(folder);
            changed = GeneratedFile.WriteIfChanged(LogTag, filePath, code);

            // Same code but a stale hash on the object: an earlier binding failed or never ran (the object was
            // saved with old references). Bind again, otherwise the object never catches up with its code.
            var isBoundStale = boundHash != structureHash;

            if (changed || bindUnchanged || isBoundStale)
            {
                HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest
                {
                    AssetPath = assetPath,
                    ObjectPath = objectPath,
                    ObjectNames = HierarchyBindingsTarget.NamePath(root),
                    TypeName = typeName,
                    Namespace = namespaceName,
                    IsPrefabAsset = isPrefab,
                    IsSceneService = isSceneService,
                    IsEntityComponent = isEntityComponent,
                    StructureHash = structureHash
                });
            }

            return true;
        }

        // The file of a class is named by the short type name: classes of one name from different namespaces
        // (Menu/Shop.prefab and GamePlay/Shop.prefab, both ShopBindings) that resolve to one folder share it.
        internal static string ToFilePath(string folder, string typeName)
        {
            return $"{folder}/{typeName}.g.cs";
        }

        // The file at the path is generated for another class: writing would silently replace that class.
        private static string FindFileConflict(string filePath, string typeName, string namespaceName)
        {
            var fullPath = CatalogPaths.ToFullPath(filePath);

            if (File.Exists(fullPath) == false)
                return null;

            var match = DeclaredNamespace.Match(File.ReadAllText(fullPath));
            var declared = match.Success ? match.Groups[1].Value : string.Empty;

            if (string.Equals(declared, namespaceName, StringComparison.Ordinal))
                return null;

            var declaredName = string.IsNullOrEmpty(declared) ? typeName : declared + "." + typeName;
            return $"{filePath} already holds {declaredName} of another object. Give these bindings another name.";
        }

        // Without code changes there is no compilation, so DidReloadScripts will not fire either,
        // hence we bind right away. With changes we set the "awaiting reload" mark: until the reload the
        // requests must not be bound - the domain still has the old classes without the new fields.
        public static void Flush(bool changed)
        {
            if (changed)
            {
                SessionState.SetBool(AwaitingReloadKey, true);
                AssetDatabase.Refresh();
                return;
            }

            EditorApplication.delayCall += ProcessPending;
        }

        // The entry point for MCP and an unfocused editor: there delayCall does not tick, and the binding
        // after compilation does not start by itself. Safe to call any number of times - while compiling or
        // awaiting the reload it does nothing. Returns the state.
        public static string PumpPending()
        {
            if (EditorApplication.isCompiling || SessionState.GetBool(AwaitingReloadKey, false))
                return "Waiting for compilation. " + HierarchyBindingsBatch.Status();

            ProcessPending();

            if (HierarchyBindingsQueue.IsEmpty() == false)
                return "Pending requests remain. " + HierarchyBindingsBatch.Status();

            return HierarchyBindingsBatch.Status();
        }

        // Requests or the awaiting mark are left (for example, the generated code failed to compile and the reload
        // never came): only a reset (Cancel) moves on.
        internal static bool HasPending()
        {
            return HierarchyBindingsQueue.HasData() || SessionState.GetBool(AwaitingReloadKey, false);
        }

        // Resets a stuck state (for example, the generated code failed to compile): the queue and the awaiting mark.
        internal static void ResetPending()
        {
            HierarchyBindingsQueue.Clear();
            SessionState.EraseBool(AwaitingReloadKey);
        }

        // The bindings of this class on the object, if any: the existing component may be a user class
        // that inherits the generated one.
        private static ObjectBindings FindBindings(GameObject root, string typeName, string namespaceName)
        {
            foreach (var bindings in root.GetComponents<ObjectBindings>())
            {
                if (IsGeneratedAs(bindings, typeName, namespaceName))
                    return bindings;
            }

            return null;
        }

        [InitializeOnLoadMethod]
        private static void InstallRegenerateHandler()
        {
            ObjectBindings.RegenerateHandler = Regenerate;
        }

        private static HierarchyBindingsNode Scan(
            GameObject root,
            string typeName,
            string namespaceName,
            string assetPath,
            bool isPrefab,
            bool isSceneService,
            bool isEntityComponent,
            List<string> errors)
        {
            if (ValidateLayer(root, typeName, namespaceName, assetPath, errors) == false)
                return null;

            var layer = HierarchyBindingsLayer.Resolve(assetPath, isPrefab);
            return HierarchyBindingsScanner.Scan(root, typeName, layer, errors, isSceneService, isEntityComponent);
        }

        // Bindings already on the object under this name are regenerated only in the asset where they
        // were declared. Base bindings seen from a variant would otherwise be written into the variant as
        // overrides of every reference, and prefab bindings seen from a scene - as overrides of the instance.
        private static bool ValidateLayer(
            GameObject root,
            string typeName,
            string namespaceName,
            string assetPath,
            List<string> errors)
        {
            foreach (var bindings in root.GetComponents<ObjectBindings>())
            {
                if (IsGeneratedAs(bindings, typeName, namespaceName) == false)
                    continue;

                var declaringAsset = HierarchyBindingsLayer.ResolveDeclaringAsset(bindings, assetPath);

                if (declaringAsset == assetPath)
                    continue;

                errors.Add($"{typeName} on '{root.name}' is declared in {declaringAsset}. Open it and regenerate there.");
                return false;
            }

            return true;
        }

        private static bool IsGeneratedAs(ObjectBindings bindings, string typeName, string namespaceName)
        {
            var type = ResolveGeneratedType(bindings);

            return type != null &&
                   string.Equals(type.Name, typeName, StringComparison.Ordinal) &&
                   string.Equals(type.Namespace ?? string.Empty, namespaceName, StringComparison.Ordinal);
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            SessionState.EraseBool(AwaitingReloadKey);

            if (HierarchyBindingsQueue.IsEmpty())
                return;

            EditorApplication.delayCall += ProcessPending;
        }

        private static void ProcessPending()
        {
            // An old delayCall may outlive a manual pump and arrive when the next phase of the batch
            // has already written its code and awaits compilation.
            if (EditorApplication.isCompiling || SessionState.GetBool(AwaitingReloadKey, false))
                return;

            var requests = HierarchyBindingsQueue.Load();

            if (requests.Count == 0)
                return;

            var bound = 0;

            // The queue is consumed only after the pass: a request that throws must not take the rest with it
            // silently. An exception ends the batch and drops what is pending, loudly: the next pass would hit it
            // again, and a batch waiting for this pass would never move on.
            try
            {
                foreach (var request in requests)
                {
                    if (Process(request))
                        bound++;
                }
            }
            catch (Exception exception)
            {
                HierarchyBindingsBatch.Abort();
                throw new InvalidOperationException(
                    $"[{LogTag}] Binding failed, the pending bindings and the prefab batch are reset: " +
                    exception.Message, exception);
            }

            HierarchyBindingsQueue.Clear();

            if (bound > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[{LogTag}] Bound {bound} of {requests.Count} bindings.");
            }

            HierarchyBindingsBatch.OnPendingProcessed();
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

        private static void Report(List<string> errors, UnityEngine.Object context)
        {
            foreach (var error in errors)
                Debug.LogError($"[{LogTag}] {error}", context);
        }
    }
}