using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Internal
{
    // Checks the lazy references of a built content directory: every Loadable / LoadableObjectId and LoadableSceneId in
    // a script object of an asset of the directory is set and points at an object or a scene the directory holds. An
    // empty reference or one that leads outside (a deleted asset, an asset the build left out) fails the build with
    // the list of them all; so does a ContentReference / ContentScene whose GUID and local id differ from its id. Script
    // objects only: nothing else can hold such a field. In the text of a scene only the ids are checked.
    public static class ContentReferenceCheck
    {
        // A LoadableObjectId in the text of a scene: {fileID: N, loadable: 1, guid: G, type: T}; a LoadableSceneId: m_SceneGUID: G.
        // Only in the documents of script objects (!u!114): the OcclusionCullingSettings of every scene has an m_SceneGUID too.
        private static readonly Regex ScriptDocument = new(@"^--- !u!114 ", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex SceneObjectId = new(@"\{fileID: (-?\d+), loadable: 1(?:, guid: ([0-9a-f]{32}))?", RegexOptions.Compiled);
        private static readonly Regex SceneSceneId = new(@"m_SceneGUID: ([0-9a-f]{32})", RegexOptions.Compiled);
        private const string EmptyGuid = "00000000000000000000000000000000";

        private const string ObjectIdType = nameof(LoadableObjectId);
        private const string SceneIdType = nameof(LoadableSceneId);

        public static void Run(ContentManifest manifest)
        {
            var errors = new List<string>();
            var objects = new HashSet<string>(manifest.SerializedFiles.Where(file => file.IsInDirectory).Select(file => file.Guid));
            var scenes = new HashSet<string>(manifest.Scenes.Select(scene => scene.Guid));

            foreach (var path in AssetPaths(manifest))
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is MonoBehaviour || asset is ScriptableObject)
                        Check(asset, path, objects, scenes, errors);
                }
            }

            foreach (var scene in manifest.Scenes)
                CheckSceneText(scene.Path, objects, scenes, errors);

            if (errors.Count > 0)
                throw new BuildFailedException(
                    $"[ContentBuild] The content directory {manifest.Name} has {errors.Count} broken lazy references:\n{string.Join("\n", errors)}");
        }

        // The assets of the directory, scenes aside: the objects of a scene are not loaded without opening it, so a scene
        // is checked in its text (the asset serialization of a project is text).
        private static IEnumerable<string> AssetPaths(ContentManifest manifest)
        {
            return manifest.SerializedFiles
                .Where(file => file.IsInDirectory && file.Kind != ContentFileKind.MonoScript)
                .Select(file => AssetDatabase.GUIDToAssetPath(file.Guid))
                .Where(path => string.IsNullOrEmpty(path) == false && path.EndsWith(".unity") == false)
                .Distinct()
                .OrderBy(path => path, System.StringComparer.Ordinal);
        }

        private static void Check(Object asset, string path, HashSet<string> objects, HashSet<string> scenes, List<string> errors)
        {
            using var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator();
            var enter = true;

            while (property.Next(enter))
            {
                enter = property.propertyType != SerializedPropertyType.String;

                if (property.type == ObjectIdType)
                {
                    enter = false;
                    CheckObject((LoadableObjectId)property.boxedValue, Where(), objects, errors);
                    CheckObjectKeys(serialized, property, Where(), errors);
                }
                else if (property.type == SceneIdType)
                {
                    enter = false;
                    CheckScene((LoadableSceneId)property.boxedValue, Where(), scenes, errors);
                    CheckSceneKeys(serialized, property, Where(), errors);
                }
            }

            string Where()
            {
                return $"{path} ({asset.GetType().Name} {asset.name}) {property.propertyPath}";
            }
        }

        // The GUID and the local id a ContentReference keeps for the loader follow its id: a reference assigned around the
        // editor (a script, a merge) would load fine and fetch the files of another object.
        private static void CheckObjectKeys(SerializedObject serialized, SerializedProperty id, string where, List<string> errors)
        {
            var suffix = $".{ContentReferenceFields.Loadable}.{ContentReferenceFields.ObjectId}";

            if (id.propertyPath.EndsWith(suffix) == false)
                return;

            var reference = serialized.FindProperty(id.propertyPath.Substring(0, id.propertyPath.Length - suffix.Length));

            if (reference.type.StartsWith(nameof(ContentReference)) == false)
                return;

            var (guid, localId) = ContentReferenceFields.Deconstruct((LoadableObjectId)id.boxedValue);
            var keptGuid = reference.FindPropertyRelative(ContentReferenceFields.Guid).stringValue;
            var keptLocalId = reference.FindPropertyRelative(ContentReferenceFields.LocalId).longValue;

            if (keptGuid != guid || keptLocalId != localId)
                errors.Add($"{where}: the reference keeps {keptGuid}:{keptLocalId}, its id is {guid}:{localId} (assign it in the inspector)");
        }

        private static void CheckSceneKeys(SerializedObject serialized, SerializedProperty id, string where, List<string> errors)
        {
            var suffix = $".{ContentReferenceFields.SceneId}";

            if (id.propertyPath.EndsWith(suffix) == false)
                return;

            var scene = serialized.FindProperty(id.propertyPath.Substring(0, id.propertyPath.Length - suffix.Length));

            if (scene.type != nameof(ContentScene))
                return;

            var guid = ContentReferenceFields.SceneGuid((LoadableSceneId)id.boxedValue);
            var keptGuid = scene.FindPropertyRelative(ContentReferenceFields.Guid).stringValue;

            if (keptGuid != guid)
                errors.Add($"{where}: the scene keeps {keptGuid}, its id is {guid} (assign it in the inspector)");
        }

        private static void CheckObject(LoadableObjectId id, string where, HashSet<string> objects, List<string> errors)
        {
            if (id.IsValid == false || LoadableObjectIdEditorUtility.TryDeconstructLoadableObjectId(id, out var guid, out _, out _) == false)
            {
                errors.Add($"{where}: empty");
                return;
            }

            if (objects.Contains(guid.ToString()) == false)
                errors.Add($"{where}: {Describe(guid.ToString())} is not in the directory");
        }

        private static void CheckScene(LoadableSceneId id, string where, HashSet<string> scenes, List<string> errors)
        {
            if (id.IsValid == false)
            {
                errors.Add($"{where}: empty");
                return;
            }

            var guid = LoadableSceneIdEditorUtility.LoadableSceneIdToGuid(id).ToString();

            if (scenes.Contains(guid) == false)
                errors.Add($"{where}: the scene {Describe(guid)} is not in the directory");
        }

        private static void CheckSceneText(string path, HashSet<string> objects, HashSet<string> scenes, List<string> errors)
        {
            var text = File.ReadAllText(path);

            foreach (Match document in ScriptDocument.Matches(text))
                CheckSceneDocument(path, text, document, objects, scenes, errors);
        }

        private static void CheckSceneDocument(string path, string text, Match document, HashSet<string> objects, HashSet<string> scenes,
            List<string> errors)
        {
            var start = document.Index;
            var end = text.IndexOf("\n--- ", start + 1, System.StringComparison.Ordinal);
            var length = (end < 0 ? text.Length : end) - start;

            foreach (Match match in SceneObjectId.Matches(text.Substring(start, length)))
            {
                var where = $"{path} line {Line(text, start + match.Index)}";
                var guid = match.Groups[2].Value;

                if (match.Groups[1].Value == "0" || guid.Length == 0 || guid == EmptyGuid)
                    errors.Add($"{where}: empty");
                else if (objects.Contains(guid) == false)
                    errors.Add($"{where}: {Describe(guid)} is not in the directory");
            }

            foreach (Match match in SceneSceneId.Matches(text.Substring(start, length)))
            {
                var where = $"{path} line {Line(text, start + match.Index)}";
                var guid = match.Groups[1].Value;

                if (guid == EmptyGuid)
                    errors.Add($"{where}: empty");
                else if (scenes.Contains(guid) == false)
                    errors.Add($"{where}: the scene {Describe(guid)} is not in the directory");
            }
        }

        private static int Line(string text, int index)
        {
            var line = 1;

            for (var i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string Describe(string guid)
        {
            // A deleted asset keeps its path in the database until a refresh.
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) || File.Exists(path) == false ? $"{guid} (no asset)" : $"{guid} ({path})";
        }
    }
}
