using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Internal
{
    // An asset without its own entry that is referenced by several addressable groups is copied by Addressables
    // into the bundle of each of them. Such assets become explicit entries of Shared_* groups,
    // and consumer bundles get a dependency on them instead of a copy.
    // A bundle pulls its dependencies whole, so shared assets are split by consumer phase
    // (FrameworkSettings.GroupPhases): the menu must not load a bundle with content that only the game needs.
    [NoAutoStaticsCleanup]
    public static class SharedAddressablesSync
    {
        private const string LogTag = "SharedAddressablesSync";
        private const string GroupPrefix = "Shared_";
        private const string CommonGroup = GroupPrefix + "Common";

        // Fonts, shaders and default package assets are needed by all phases.
        private static IReadOnlyList<string> CommonPaths => FrameworkSettings.SharedCommonPaths;

        // Groups loaded by only one phase (the project lists them). Everything else lives in all phases.
        // A mistake in this table does not bring duplicates back, it only makes a phase load an extra bundle.
        private static IReadOnlyDictionary<string, string> GroupPhases => FrameworkSettings.GroupPhases;

        private static readonly CatalogGenerationRunner Runner = new(LogTag, Sync);

        [MenuItem("Tools/SyncSharedAddressables")]
        public static void Generate()
        {
            Runner.Run();
        }

        public static void GenerateNow()
        {
            Runner.RunNow();
        }

        public static void ScheduleSync()
        {
            Runner.Schedule();
        }

        public static void Sync()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                Debug.LogError($"[{LogTag}] Addressable settings are missing.");
                return;
            }

            var targets = CollectTargets(settings);
            var changed = ApplyTargets(settings, targets);
            changed |= RemoveStale(settings, targets);

            if (changed == false)
                return;

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            Debug.Log($"[{LogTag}] Synced {targets.Count} shared asset(s).");
        }

        // Shared asset GUID -> Shared group name.
        private static Dictionary<string, string> CollectTargets(AddressableAssetSettings settings)
        {
            var roots = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var explicitGuids = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in settings.groups)
            {
                if (group == null || IsSharedGroup(group.Name))
                    continue;

                var paths = new List<string>();

                foreach (var entry in group.entries)
                {
                    if (entry == null)
                        continue;

                    explicitGuids.Add(entry.guid);
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
                        if (explicitGuids.Contains(AssetDatabase.AssetPathToGUID(dependency)))
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

            return targets;
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

        private static bool ApplyTargets(AddressableAssetSettings settings, Dictionary<string, string> targets)
        {
            var changed = false;

            foreach (var target in targets)
            {
                var group = CatalogAddressablesSync.GetOrCreatePackedGroup(settings, target.Value);

                if (group == null)
                {
                    Debug.LogError($"[{LogTag}] Failed to create group '{target.Value}'.");
                    continue;
                }

                var entry = settings.FindAssetEntry(target.Key);

                if (entry != null && entry.parentGroup == group)
                    continue;

                if (settings.CreateOrMoveEntry(target.Key, group, false, false) == null)
                {
                    Debug.LogError(
                        $"[{LogTag}] Failed to mark {AssetDatabase.GUIDToAssetPath(target.Key)} addressable.");
                    continue;
                }

                changed = true;
            }

            return changed;
        }

        private static bool RemoveStale(AddressableAssetSettings settings, Dictionary<string, string> targets)
        {
            var changed = false;
            var staleGroups = new List<AddressableAssetGroup>();

            foreach (var group in settings.groups)
            {
                if (group == null || IsSharedGroup(group.Name) == false)
                    continue;

                var staleEntries = new List<AddressableAssetEntry>();

                foreach (var entry in group.entries)
                {
                    if (entry != null && targets.ContainsKey(entry.guid) == false)
                        staleEntries.Add(entry);
                }

                foreach (var entry in staleEntries)
                {
                    settings.RemoveAssetEntry(entry.guid, false);
                    changed = true;
                }

                if (group.entries.Count == 0)
                    staleGroups.Add(group);
            }

            foreach (var group in staleGroups)
            {
                settings.RemoveGroup(group);
                changed = true;
            }

            return changed;
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

        private static bool IsSharedGroup(string groupName)
        {
            return groupName.StartsWith(GroupPrefix, StringComparison.Ordinal);
        }
    }
}