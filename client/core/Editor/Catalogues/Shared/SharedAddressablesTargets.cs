using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine.U2D;

namespace Internal
{
    // The assets SharedAddressablesSync makes entries of, and their groups.
    // A bundle pulls its dependencies whole, so shared assets are split by consumer phase
    // (FrameworkSettings.GroupPhases): the menu must not load a bundle with content that only the game needs.
    // A sprite atlas is not a dependency of its sprites, yet every bundle with one of the sprites gets the whole atlas with
    // its pages: an atlas with sprites used by several groups becomes an entry too, its used sprites next to it.
    internal static class SharedAddressablesTargets
    {
        private const string GroupPrefix = "Shared_";
        private const string CommonGroup = GroupPrefix + "Common";

        // Fonts, shaders and default package assets are needed by all phases.
        private static IReadOnlyList<string> CommonPaths => FrameworkSettings.SharedCommonPaths;

        // Groups loaded by only one phase (the project lists them). Everything else lives in all phases.
        // A mistake in this table does not bring duplicates back, it only makes a phase load an extra bundle.
        private static IReadOnlyDictionary<string, string> GroupPhases => FrameworkSettings.GroupPhases;

        public static bool IsSharedGroup(string groupName)
        {
            return groupName.StartsWith(GroupPrefix, StringComparison.Ordinal);
        }

        // Shared asset GUID -> Shared group name.
        public static Dictionary<string, string> Collect(AddressableAssetSettings settings)
        {
            var roots = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var entryGroups = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var group in settings.groups)
            {
                if (group == null || IsSharedGroup(group.Name))
                    continue;

                var paths = new List<string>();

                foreach (var entry in group.entries)
                {
                    if (entry == null)
                        continue;

                    entryGroups.Add(entry.guid, group.Name);
                    paths.Add(entry.AssetPath);
                }

                if (paths.Count > 0)
                    roots.Add(group.Name, paths);
            }

            var consumers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();

            foreach (var root in roots)
            {
                visited.Clear();

                foreach (var path in root.Value)
                {
                    visited.Add(path);
                    pending.Push(path);
                }

                while (pending.Count > 0)
                {
                    foreach (var dependency in AssetDatabase.GetDependencies(pending.Pop(), false))
                    {
                        // Only editor icons come through scripts, and they do not get into bundles.
                        if (visited.Add(dependency) == false || IsScript(dependency))
                            continue;

                        // An entry of another group is a dependency on its bundle; that group accounts for its own content.
                        if (entryGroups.ContainsKey(AssetDatabase.AssetPathToGUID(dependency)))
                            continue;

                        if (consumers.TryGetValue(dependency, out var groups) == false)
                        {
                            groups = new HashSet<string>(StringComparer.Ordinal);
                            consumers.Add(dependency, groups);
                        }

                        groups.Add(root.Key);
                        pending.Push(dependency);
                    }
                }
            }

            var targets = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var consumer in consumers)
            {
                if (consumer.Value.Count < 2 || CanBeEntry(consumer.Key) == false)
                    continue;

                targets.Add(AssetDatabase.AssetPathToGUID(consumer.Key), ResolveGroup(consumer.Key, consumer.Value));
            }

            AddAtlases(targets, consumers, entryGroups);
            return targets;
        }

        private static void AddAtlases(
            Dictionary<string, string> targets,
            Dictionary<string, HashSet<string>> consumers,
            Dictionary<string, string> entryGroups)
        {
            foreach (var atlasGuid in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                var atlasPath = AssetDatabase.GUIDToAssetPath(atlasGuid);

                if (CanBeEntry(atlasPath) == false)
                    continue;

                var packed = PackedTextures(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath));
                var groups = new HashSet<string>(StringComparer.Ordinal);

                foreach (var path in packed)
                {
                    if (consumers.TryGetValue(path, out var pathGroups) == true)
                        groups.UnionWith(pathGroups);

                    if (entryGroups.TryGetValue(AssetDatabase.AssetPathToGUID(path), out var entryGroup) == true)
                        groups.Add(entryGroup);
                }

                if (groups.Count < 2)
                    continue;

                var target = ResolveGroup(atlasPath, groups);
                targets[atlasGuid] = target;

                foreach (var path in packed)
                {
                    if (consumers.ContainsKey(path) == true)
                        targets[AssetDatabase.AssetPathToGUID(path)] = target;
                }
            }
        }

        private static List<string> PackedTextures(SpriteAtlas atlas)
        {
            var result = new List<string>();

            foreach (var packable in atlas.GetPackables())
            {
                var path = AssetDatabase.GetAssetPath(packable);

                if (AssetDatabase.IsValidFolder(path) == false)
                {
                    result.Add(path);
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                    result.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            return result;
        }

        private static string ResolveGroup(string path, HashSet<string> consumers)
        {
            foreach (var prefix in CommonPaths)
            {
                if (path.StartsWith(prefix, StringComparison.Ordinal))
                    return CommonGroup;
            }

            // One phase for all consumers: its own shared group; a consumer of no single phase or two phases: common.
            string phase = null;

            foreach (var consumer in consumers)
            {
                if (GroupPhases.TryGetValue(consumer, out var groupPhase) == false)
                    return CommonGroup;

                if (phase != null && string.Equals(phase, groupPhase, StringComparison.Ordinal) == false)
                    return CommonGroup;

                phase = groupPhase;
            }

            return GroupPrefix + phase;
        }

        // Mirrors AddressableAssetUtility.IsPathValidForEntry (internal) and rejects Resources:
        // an asset from there goes into the player anyway, an entry would only add a second copy.
        private static bool CanBeEntry(string path)
        {
            if (path.StartsWith("Assets/", StringComparison.Ordinal) == false &&
                path.StartsWith("Packages/", StringComparison.Ordinal) == false)
                return false;

            if (path.Contains("/Editor/") || path.Contains("/Resources/"))
                return false;

            var extension = Path.GetExtension(path);
            return extension != ".unity" && extension != ".preset" && extension != ".asmdef";
        }

        private static bool IsScript(string path)
        {
            var extension = Path.GetExtension(path);
            return extension == ".cs" || extension == ".dll";
        }
    }
}
