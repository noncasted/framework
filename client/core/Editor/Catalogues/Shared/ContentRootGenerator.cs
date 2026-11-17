using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Writes the ContentRoot of the catalogues into the content folder of the game: a lazy reference to every group asset
    // of the catalogues (env assets, prefabs, audio, sprites; the preloaded ones of the registries apart) and every scene
    // of the scene folders outside the Build Settings. Runs after every catalogue that changes its group assets, in
    // Regenerate All and before every build of the content bundles (ContentRootBuildStep). A bundle holds it only when
    // Path is among its roots: the main ContentBundle of the game (ContentOptions.Name) lists it. A build that leaves bundles
    // out (ContentBuildContext.Exclude) writes it without their roots for the time of the bundle build (GenerateWithout)
    // and then in full again.
    [NoAutoStaticsCleanup]
    public static class ContentRootGenerator
    {
        private const string LogTag = "ContentRootGenerator";

        private static readonly CatalogGenerationRunner Runner = new(LogTag, Generate);

        // The paths of the groups and scenes left out of the root being written; empty outside GenerateWithout.
        private static IReadOnlyCollection<string> _without = Array.Empty<string>();

        public static string Path => FrameworkSettings.Content("Runtime/Catalogues/ContentRoot.asset");

        [MenuItem(FrameworkMenu.Codegen + "Content Root", priority = FrameworkMenu.TrailingPriority)]
        public static void GenerateMenu()
        {
            Runner.Run();
        }

        public static void GenerateNow()
        {
            Runner.RunNow();
        }

        // Without the groups and scenes of these paths (the roots of the bundles a build leaves out); GenerateNow writes them back.
        public static void GenerateWithout(IReadOnlyCollection<string> paths)
        {
            _without = paths;

            try
            {
                Runner.RunNow();
            }
            finally
            {
                _without = Array.Empty<string>();
            }
        }

        public static void ScheduleGenerate()
        {
            Runner.Schedule();
        }

        private static void Generate()
        {
            var groups = new List<string>();
            var preloaded = new List<string>();

            Collect<EnvAssetGroupAsset>(AssetCatalogGenerator.GroupsFolder, AssetGroupsRegistry.Instance, groups, preloaded);
            Collect<PrefabGroupAsset>(PrefabCatalogGenerator.GroupsFolder, PrefabGroupsRegistry.Instance, groups, preloaded);
            Collect<AudioGroupAsset>(AudioCatalogGenerator.GroupsFolder, null, groups, preloaded);
            Collect<SpriteGroupAsset>(SpriteGenerator.GroupsFolder, null, groups, preloaded);

            var scenes = new List<string>();
            SceneGenerator.Generate(FrameworkSettings.SceneFolders,
                found => scenes.AddRange(found.Select(scene => AssetDatabase.GUIDToAssetPath(scene.sceneGuid))));
            scenes.Sort(StringComparer.Ordinal);

            groups.RemoveAll(path => _without.Contains(path));
            preloaded.RemoveAll(path => _without.Contains(path));
            scenes.RemoveAll(path => _without.Contains(path));

            var root = AssetDatabase.LoadAssetAtPath<ContentRoot>(Path);

            if (root == null)
            {
                CatalogPaths.EnsureFolder(System.IO.Path.GetDirectoryName(Path).Replace('\\', '/'));
                root = ScriptableObject.CreateInstance<ContentRoot>();
                AssetDatabase.CreateAsset(root, Path);
            }

            if (Matches(root.Groups, groups) && Matches(root.PreloadedGroups, preloaded) &&
                root.Scenes.Select(scene => scene.Guid).SequenceEqual(scenes.Select(AssetDatabase.AssetPathToGUID)))
                return;

            var serialized = new SerializedObject(root);
            WriteObjects(serialized.FindProperty("_groups"), groups);
            WriteObjects(serialized.FindProperty("_preloadedGroups"), preloaded);
            WriteScenes(serialized.FindProperty("_scenes"), scenes);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssetIfDirty(root);

            Debug.Log($"[{LogTag}] {Path}: {groups.Count} group(s), {preloaded.Count} preloaded, {scenes.Count} scene(s).");
        }

        // The group assets of one catalogue by path; a catalogue without a registry has no preloaded groups.
        private static void Collect<TAsset>(string folder, CatalogGroupsRegistry registry, List<string> groups, List<string> preloaded)
            where TAsset : ScriptableObject
        {
            if (AssetDatabase.IsValidFolder(folder) == false)
                return;

            // By the main type of every asset of the folder, not by the "t:" search: the type index of a Library imported
            // before a script moved can miss assets whose type is right (2026-11-17, mines-leader: a build left the
            // Internal group out of the root and the game threw at start).
            var paths = AssetDatabase.FindAssets(string.Empty, new[] { folder })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(TAsset))
                                     .Distinct()
                                     .OrderBy(path => path, StringComparer.Ordinal);

            foreach (var path in paths)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                var isPreloaded = registry != null && registry.IsPreloaded(name);
                (isPreloaded ? preloaded : groups).Add(path);
            }
        }

        private static bool Matches(IReadOnlyList<ContentReference<ScriptableObject>> references, List<string> paths)
        {
            return references.Select(reference => reference.Guid).SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID));
        }

        private static void WriteObjects(SerializedProperty list, List<string> paths)
        {
            list.arraySize = paths.Count;

            for (var i = 0; i < paths.Count; i++)
                ContentReferenceFields.AssignObject(list.GetArrayElementAtIndex(i), AssetDatabase.LoadAssetAtPath<ScriptableObject>(paths[i]));
        }

        private static void WriteScenes(SerializedProperty list, List<string> paths)
        {
            list.arraySize = paths.Count;

            for (var i = 0; i < paths.Count; i++)
                ContentReferenceFields.AssignScene(list.GetArrayElementAtIndex(i), paths[i]);
        }
    }
}
