using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // Bindings break silently: the hierarchy was edited without Regenerate, a variant removed a component
    // the base looks at, entity bindings did not get into the list of the view. The validator finds all of
    // that and reports it loudly with the path, fixing nothing.
    //
    // The structure (StructureHash) is checked only for bindings of the own layer: base bindings on a
    // variant are checked together with the base itself, here only their references are checked - this is
    // how a variant that removed a component of the base is caught.
    public static class HierarchyBindingsValidator
    {
        private const string LogTag = "HierarchyBindingsValidator";

        private const BindingFlags DeclaredFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Returns the number of errors; each one is already in the console.
        public static int ValidatePrefabs(IEnumerable<string> assetPaths)
        {
            var errors = 0;

            foreach (var assetPath in assetPaths)
                errors += ValidatePrefab(assetPath);

            return errors;
        }

        public static int ValidatePrefab(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (asset == null)
                throw new InvalidOperationException($"Prefab {assetPath} is not loadable.");

            var contents = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                var layer = HierarchyBindingsLayer.Resolve(assetPath, true);
                return Validate(new[] { contents }, assetPath, layer, asset);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        public static int ValidateScene(Scene scene)
        {
            if (scene.IsValid() == false || scene.isLoaded == false)
                throw new InvalidOperationException($"Scene '{scene.name}' is not loaded.");

            var layer = HierarchyBindingsLayer.Resolve(scene.path, false);
            return Validate(scene.GetRootGameObjects(), scene.path, layer, null);
        }

        private static int Validate(
            GameObject[] roots,
            string assetPath,
            HierarchyBindingsLayer layer,
            UnityEngine.Object assetContext)
        {
            var errors = 0;

            foreach (var root in roots)
            {
                foreach (var bindings in root.GetComponentsInChildren<ObjectBindings>(true))
                {
                    var problems = new List<string>();
                    ValidateBindings(bindings, assetPath, layer, problems);

                    // The prefab contents are unloaded right after the check, so the context is the asset
                    // itself, and the object inside it is named by its path.
                    var context = assetContext != null ? assetContext : bindings;
                    var location = $"{assetPath} > {PathOf(bindings.transform)} ({bindings.GetType().Name})";

                    foreach (var problem in problems)
                        Debug.LogError($"[{LogTag}] {location}: {problem}", context);

                    errors += problems.Count;
                }
            }

            return errors;
        }

        private static void ValidateBindings(
            ObjectBindings bindings,
            string assetPath,
            HierarchyBindingsLayer layer,
            List<string> problems)
        {
            var type = HierarchyBindingsGenerator.ResolveGeneratedType(bindings);

            if (type == null)
            {
                problems.Add("does not derive from a generated bindings class.");
                return;
            }

            ValidateFlags(bindings, problems);
            ValidateReferences(bindings, type, problems);
            ValidateEntityRegistration(bindings, problems);
            ValidateSceneRegistration(bindings, layer, problems);

            var declaringAsset = HierarchyBindingsLayer.ResolveDeclaringAsset(bindings, assetPath);

            if (declaringAsset != assetPath)
                return;

            ValidateStructure(bindings, type, layer, problems);
        }

        // The checkboxes on the component must match the interfaces that are actually generated.
        private static void ValidateFlags(ObjectBindings bindings, List<string> problems)
        {
            if (bindings.IsEntityComponent != bindings is IEntityComponent)
                problems.Add("'Is Entity Component' differs from the generated code. Regenerate.");

            if (bindings.IsSceneService != bindings is ISceneService)
                problems.Add("'Is Scene Service' differs from the generated code. Regenerate.");
        }

        // Any empty reference of the generated class is an error: the generator fills every field, so null
        // means the object or the component is gone (typically - a variant removed a component of the base).
        private static void ValidateReferences(ObjectBindings bindings, Type generatedType, List<string> problems)
        {
            var serializedObject = new SerializedObject(bindings);

            foreach (var field in generatedType.GetFields(DeclaredFields))
            {
                if (field.IsDefined(typeof(SerializeField)) == false)
                    continue;

                var property = serializedObject.FindProperty(field.Name);

                if (property == null)
                {
                    problems.Add($"generated field '{field.Name}' is not serialized. Recompile and regenerate.");
                    continue;
                }

                CollectNulls(property, problems);
            }
        }

        private static void CollectNulls(SerializedProperty root, List<string> problems)
        {
            // The walk is limited to the subtree of the field: everything under it has a path with its prefix.
            var current = root.Copy();
            var prefix = root.propertyPath;

            do
            {
                if (current.propertyPath != prefix &&
                    current.propertyPath.StartsWith(prefix + ".", StringComparison.Ordinal) == false)
                    break;

                if (current.propertyType == SerializedPropertyType.ObjectReference &&
                    current.objectReferenceValue == null)
                    problems.Add($"reference '{current.propertyPath}' is null. The object or component is " +
                                 "missing (removed in a variant, renamed or deleted without Regenerate).");
            }
            while (current.Next(true));
        }

        private static void ValidateEntityRegistration(ObjectBindings bindings, List<string> problems)
        {
            if (bindings is IEntityComponent == false)
                return;

            // A base prefab may have no view - every variant adds its own, and the list is checked there.
            // No view at all is not an error here.
            foreach (var view in bindings.GetComponentsInParent<ScopeEntityView>(true))
            {
                if (HierarchyBindingsEntityViews.Current(view).Contains(bindings))
                    continue;

                problems.Add($"is not listed in {view.GetType().Name}._autoDetected on '{view.name}'. " +
                             "Press Scan on the view or regenerate the prefab batch.");
            }
        }

        // Scene bindings in a scene must be in the list of its SceneServicesFactory. A prefab has no factory:
        // the list is built by the scene the prefab is placed in.
        private static void ValidateSceneRegistration(
            ObjectBindings bindings,
            HierarchyBindingsLayer layer,
            List<string> problems)
        {
            if (bindings is ISceneService == false || layer.IsScene == false)
                return;

            if (HierarchyBindingsEntityViews.IsSceneServiceListed(bindings.gameObject.scene, bindings))
                return;

            problems.Add("is not listed in SceneServicesFactory._services. Run 'Assets/Scan services'.");
        }

        private static void ValidateStructure(
            ObjectBindings bindings,
            Type generatedType,
            HierarchyBindingsLayer layer,
            List<string> problems)
        {
            var scanErrors = new List<string>();
            var node = HierarchyBindingsScanner.Scan(bindings.gameObject, generatedType.Name, layer, scanErrors);

            if (node == null)
            {
                foreach (var error in scanErrors)
                    problems.Add("scan failed: " + error);

                return;
            }

            var hash = HierarchyBindingsStructure.ComputeHash(node);

            if (hash != bindings.StructureHash)
                problems.Add($"structure is stale (stored {bindings.StructureHash}, live {hash}). " +
                             "The hierarchy changed after generation: Regenerate.");
        }

        private static string PathOf(Transform target)
        {
            var names = new List<string>();

            for (var current = target; current != null; current = current.parent)
                names.Add(current.name);

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
