using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // One window for both the first generation and Regenerate. An object may carry several bindings of
    // different classes (base and variant of a prefab), so the window shows every class already on it with
    // its own Regenerate button and below offers to create one more under a new name. The name and the
    // namespace of the existing ones are not editable, so no second class is created.
    public sealed class HierarchyBindingsWindow : EditorWindow
    {
        private readonly List<ExistingBindings> _existing = new();

        private GameObject _target;
        private string _assetPath;
        private string _bindingsName;
        private string _namespace;
        private bool _isSceneService;
        private bool _isEntityComponent;

        public static void Open(GameObject target)
        {
            var window = CreateInstance<HierarchyBindingsWindow>();
            window.titleContent = new GUIContent("Object Bindings");
            window.Setup(target);
            window.minSize = new Vector2(460f, 200f);
            window.ShowUtility();
        }

        private void Setup(GameObject target)
        {
            _target = target;
            _assetPath = HierarchyBindingsTarget.TryDescribe(target, out var assetPath, out _, out _, out _)
                ? assetPath
                : string.Empty;

            foreach (var bindings in target.GetComponents<ObjectBindings>())
            {
                var type = HierarchyBindingsGenerator.ResolveGeneratedType(bindings);

                if (type == null)
                    throw new InvalidOperationException(
                        $"{bindings.GetType().Name} on '{target.name}' does not derive from a generated class.");

                _existing.Add(new ExistingBindings
                {
                    Type = type,
                    LayerAssetPath = DeclaringAsset(bindings, _assetPath),
                    IsForeign = IsForeignLayer(bindings, _assetPath),
                    IsSceneService = bindings.IsSceneService,
                    IsEntityComponent = bindings.IsEntityComponent
                });
            }

            _bindingsName = HierarchyBindingsNaming.ToIdentifier(target.name);
            _namespace = _existing.Count > 0
                ? _existing[0].Type.Namespace ?? string.Empty
                : HierarchyBindingsPaths.ResolveDefaultNamespace(target);
        }

        // The layer is the asset that declares the component, and PrefabUtility knows it: the GUID stored at binding
        // stays the one of the original in a duplicated prefab (Ctrl+D), whose bindings are its own.
        internal static bool IsForeignLayer(ObjectBindings bindings, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            return DeclaringAsset(bindings, assetPath) != assetPath;
        }

        private static string DeclaringAsset(ObjectBindings bindings, string assetPath)
        {
            return HierarchyBindingsLayer.ResolveDeclaringAsset(bindings, assetPath);
        }

        private void OnGUI()
        {
            if (_target == null)
            {
                Close();
                return;
            }

            EditorGUILayout.LabelField("Object", _target.name);

            if (_existing.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Existing Bindings", EditorStyles.boldLabel);

                foreach (var existing in _existing)
                {
                    if (DrawExisting(existing))
                        return;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(_existing.Count > 0 ? "Add Bindings" : "New Bindings", EditorStyles.boldLabel);
            DrawNew();
        }

        // Returns true if the window is closed and nothing more may be drawn.
        private bool DrawExisting(ExistingBindings existing)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Class", existing.Type.FullName);

                if (string.IsNullOrEmpty(existing.LayerAssetPath) == false)
                    EditorGUILayout.LabelField("Layer", existing.LayerAssetPath);

                if (existing.IsForeign)
                {
                    EditorGUILayout.HelpBox(
                        $"Declared in {existing.LayerAssetPath}. Open that asset to regenerate these bindings.",
                        MessageType.Info);
                    return false;
                }

                existing.IsSceneService = EditorGUILayout.Toggle("Is Scene Service", existing.IsSceneService);
                existing.IsEntityComponent = EditorGUILayout.Toggle("Is Entity Component", existing.IsEntityComponent);

                if (GUILayout.Button("Regenerate") == false)
                    return false;
            }

            var target = _target;
            Close();
            HierarchyBindingsGenerator.Generate(
                    target,
                    existing.Type.Name,
                    existing.Type.Namespace ?? string.Empty,
                    existing.IsSceneService,
                    existing.IsEntityComponent
                );
            return true;
        }

        private void DrawNew()
        {
            _bindingsName = EditorGUILayout.TextField("Bindings Name", _bindingsName);
            _namespace = EditorGUILayout.TextField("Namespace", _namespace);
            _isSceneService = EditorGUILayout.Toggle("Is Scene Service", _isSceneService);
            _isEntityComponent = EditorGUILayout.Toggle("Is Entity Component", _isEntityComponent);

            var typeName = HierarchyBindingsGenerator.ToTypeName(_bindingsName);
            EditorGUILayout.LabelField("Class", string.IsNullOrEmpty(typeName) ? "<invalid name>" : typeName);

            var problem = FindNameProblem(typeName);

            if (string.IsNullOrEmpty(problem) == false)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(problem) == false))
            {
                if (GUILayout.Button("Generate") == false)
                    return;
            }

            var target = _target;
            var name = _bindingsName;
            var namespaceName = _namespace;
            var isSceneService = _isSceneService;
            var isEntityComponent = _isEntityComponent;
            Close();
            HierarchyBindingsGenerator.Generate(target, name, namespaceName, isSceneService, isEntityComponent);
        }

        // A new class must match neither the bindings on this object (they have Regenerate) nor another
        // class of the project: the generator would overwrite its file.
        private string FindNameProblem(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return "Name is not a C# identifier.";

            var fullName = string.IsNullOrEmpty(_namespace) ? typeName : _namespace + "." + typeName;

            foreach (var existing in _existing)
            {
                if (existing.Type.FullName == fullName)
                    return $"{typeName} is already on this object. Use its Regenerate button above.";
            }

            foreach (var type in TypeCache.GetTypesDerivedFrom<ObjectBindings>())
            {
                if (type.FullName == fullName)
                    return $"{fullName} already exists for another object. Pick another name.";
            }

            return string.Empty;
        }

        private sealed class ExistingBindings
        {
            public Type Type;
            public string LayerAssetPath;
            public bool IsForeign;
            public bool IsSceneService;
            public bool IsEntityComponent;
        }
    }
}