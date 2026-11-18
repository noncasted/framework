using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // Reads the lazy references (ContentReference<T>, ContentScene) an asset or a scene holds: in its script objects (the
    // only ones that can have such a field), found by the serialized type of the field. A file whose text has no "_guid:" is not
    // opened (the asset serialization of a project is text); a scene is opened as a preview scene for the look and closed again.
    // Each path is read once per instance.
    public sealed class ContentLazyReferenceScanner
    {
        private const string Marker = ContentReferenceFields.Guid + ":";

        private readonly Dictionary<string, IReadOnlyList<ContentLazyReference>> _byPath = new();

        public IReadOnlyList<ContentLazyReference> Of(string path)
        {
            if (_byPath.TryGetValue(path, out var references))
                return references;

            references = HasMarker(path) == false ? Array.Empty<ContentLazyReference>() : path.EndsWith(".unity", StringComparison.Ordinal) ? OfScene(path) : OfAsset(path);
            _byPath.Add(path, references);
            return references;
        }

        // A field of a script object: null when it is no lazy reference.
        public static ContentLazyReference? Read(SerializedProperty property, string where)
        {
            // A list of references has the type of its elements: its elements are read one by one.
            if (property.propertyType != SerializedPropertyType.Generic || property.isArray)
                return null;

            if (property.type == nameof(ContentScene))
            {
                var sceneGuid = property.FindPropertyRelative(ContentReferenceFields.Guid);
                return sceneGuid == null ? null : new ContentLazyReference(where, sceneGuid.stringValue, 0, true);
            }

            if (property.type.StartsWith(nameof(ContentReference), StringComparison.Ordinal) == false)
                return null;

            var guid = property.FindPropertyRelative(ContentReferenceFields.Guid);
            var localId = property.FindPropertyRelative(ContentReferenceFields.LocalId);

            if (guid == null || localId == null)
                return null;

            return new ContentLazyReference(where, guid.stringValue, localId.longValue, false);
        }

        private static bool HasMarker(string path)
        {
            if (File.Exists(path) == false)
                return false;

            // Binary assets (textures, audio) are never read whole.
            if (path.EndsWith(".asset", StringComparison.Ordinal) == false && path.EndsWith(".prefab", StringComparison.Ordinal) == false &&
                path.EndsWith(".unity", StringComparison.Ordinal) == false)
                return false;

            return File.ReadAllText(path).Contains(Marker, StringComparison.Ordinal);
        }

        private static IReadOnlyList<ContentLazyReference> OfAsset(string path)
        {
            var references = new List<ContentLazyReference>();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is MonoBehaviour || asset is ScriptableObject)
                    Collect(asset, path, references);
            }

            return references;
        }

        private static IReadOnlyList<ContentLazyReference> OfScene(string path)
        {
            // A preview scene: the scenes open in the editor stay as they are (an untitled one refuses an additive scene).
            var scene = EditorSceneManager.OpenPreviewScene(path);
            var references = new List<ContentLazyReference>();

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour != null)
                            Collect(behaviour, path, references);
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            return references;
        }

        private static void Collect(Object owner, string path, List<ContentLazyReference> references)
        {
            using var serialized = new SerializedObject(owner);
            var property = serialized.GetIterator();
            var enter = true;

            while (property.Next(enter))
            {
                enter = property.propertyType != SerializedPropertyType.String;
                var reference = Read(property, $"{path} ({owner.GetType().Name} {owner.name}) {property.propertyPath}");

                if (reference == null)
                    continue;

                enter = false;
                references.Add(reference.Value);
            }
        }
    }
}
