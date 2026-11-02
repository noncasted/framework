using System.Collections.Generic;
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
    // Which assets and which groups: SharedAddressablesTargets.
    [NoAutoStaticsCleanup]
    public static class SharedAddressablesSync
    {
        private const string LogTag = "SharedAddressablesSync";

        private static readonly CatalogGenerationRunner Runner = new(LogTag, Sync);

        [MenuItem(FrameworkMenu.Codegen + "Sync Shared Addressables", priority = FrameworkMenu.TrailingPriority)]
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
            if (FrameworkSettings.WithoutAddressables)
                return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                Debug.LogError($"[{LogTag}] Addressable settings are missing.");
                return;
            }

            var targets = SharedAddressablesTargets.Collect(settings);
            var changed = ApplyTargets(settings, targets);
            changed |= RemoveStale(settings, targets);

            if (changed == false)
                return;

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            Debug.Log($"[{LogTag}] Synced {targets.Count} shared asset(s).");
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
                if (group == null || SharedAddressablesTargets.IsSharedGroup(group.Name) == false)
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
    }
}