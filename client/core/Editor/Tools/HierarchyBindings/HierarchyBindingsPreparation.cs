using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Internal
{
    // The first phase of the generation: the object is scanned, its code is written, the request waits for the reload.
    internal static class HierarchyBindingsPreparation
    {
        private const string LogTag = HierarchyBindingsLog.Tag;

        // The namespace line HierarchyBindingsCodeGenerator writes; a class without a namespace has none.
        private static readonly Regex DeclaredNamespace = new(@"^namespace (\S+) \{", RegexOptions.Multiline);

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

            var typeName = HierarchyBindingsGeneratedTypes.ToTypeName(bindingsName);

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
                    boundHash = HierarchyBindingsGeneratedTypes.FindBindings(target.Root, typeName, namespaceName)?.StructureHash;
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
                HierarchyBindingsLog.Report(errors, root);
                return false;
            }

            var folder = HierarchyBindingsPaths.ResolveGeneratedFolder(root, assetPath);
            var filePath = HierarchyBindingsPaths.ToFilePath(folder, typeName);
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
                if (HierarchyBindingsGeneratedTypes.IsGeneratedAs(bindings, typeName, namespaceName) == false)
                    continue;

                var declaringAsset = HierarchyBindingsLayer.ResolveDeclaringAsset(bindings, assetPath);

                if (declaringAsset == assetPath)
                    continue;

                errors.Add($"{typeName} on '{root.name}' is declared in {declaringAsset}. Open it and regenerate there.");
                return false;
            }

            return true;
        }
    }
}
