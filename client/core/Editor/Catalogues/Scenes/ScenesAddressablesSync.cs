using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Internal
{
    // StaticScene loads scenes by GUID, so a scene without an Addressables entry fails with InvalidKeyException.
    // Scenes of one domain are always loaded together and live in one `Scenes_<domain>` group. The domain of a scene
    // under a module root (FrameworkSettings.ModuleRoots) is the folder name of the root, so modules under one parent
    // folder (Assets/Game/Engine, Assets/Game/Menu) get their own groups; any other scene takes its folder under Assets/.
    // Build Settings scenes are already in the player: in Addressables they would get into the build a second time with their dependencies.
    public static class ScenesAddressablesSync
    {
        private const string LogTag = "ScenesAddressablesSync";
        private const string GroupPrefix = "Scenes_";
        private const string LegacyGroupName = "Scenes";

        public static void Sync(IReadOnlyList<(string sceneName, string sceneGuid)> scenes)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // A project without scenes needs no Addressables; one whose last scene is gone still has its entries.
            if (settings == null && scenes.Count == 0)
                return;

            if (settings == null)
            {
                Debug.LogError($"[{LogTag}] Addressable settings are missing.");
                return;
            }

            var builtIn = CollectBuiltInScenes();
            var used = new HashSet<string>(StringComparer.Ordinal);
            var usedGroups = new HashSet<string>(StringComparer.Ordinal);
            var changed = false;

            foreach (var (_, guid) in scenes)
            {
                if (builtIn.Contains(guid))
                    continue;

                var address = AssetDatabase.GUIDToAssetPath(guid);
                var groupName = GroupPrefix + GetDomain(address);
                var group = CatalogAddressablesSync.GetOrCreatePackedGroup(settings, groupName);

                if (group == null)
                {
                    Debug.LogError($"[{LogTag}] Failed to create group '{groupName}'.");
                    continue;
                }

                used.Add(guid);
                usedGroups.Add(groupName);

                var entry = settings.FindAssetEntry(guid);

                if (entry == null || entry.parentGroup != group)
                {
                    entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    changed = true;

                    if (entry == null)
                    {
                        Debug.LogError($"[{LogTag}] Failed to mark {address} addressable.");
                        continue;
                    }
                }

                if (entry.address != address)
                {
                    entry.SetAddress(address, false);
                    changed = true;
                }
            }

            changed |= RemoveStale(settings, used, usedGroups);

            // Scenes are dependency roots, so shared assets are recalculated after them.
            SharedAddressablesSync.ScheduleSync();

            if (changed == false)
                return;

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            Debug.Log($"[{LogTag}] Synced {used.Count} scene(s) into {usedGroups.Count} group(s).");
        }

        private static HashSet<string> CollectBuiltInScenes()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    result.Add(scene.guid.ToString());
            }

            return result;
        }

        private static string GetDomain(string assetPath)
        {
            if (ModuleAssetLayout.TryGetRoot(assetPath, out var root))
                return root.Substring(root.LastIndexOf('/') + 1);

            var parts = assetPath.Split('/');
            return parts.Length > 2 ? parts[1] : "Common";
        }

        private static bool RemoveStale(
            AddressableAssetSettings settings,
            HashSet<string> used,
            HashSet<string> usedGroups)
        {
            var changed = false;
            var staleGroups = new List<AddressableAssetGroup>();

            foreach (var group in settings.groups)
            {
                if (group == null || IsSceneGroup(group.Name) == false)
                    continue;

                var staleEntries = new List<AddressableAssetEntry>();

                foreach (var entry in group.entries)
                {
                    if (entry != null && used.Contains(entry.guid) == false)
                        staleEntries.Add(entry);
                }

                foreach (var entry in staleEntries)
                {
                    settings.RemoveAssetEntry(entry.guid, false);
                    changed = true;
                }

                if (usedGroups.Contains(group.Name) == false)
                    staleGroups.Add(group);
            }

            foreach (var group in staleGroups)
            {
                settings.RemoveGroup(group);
                changed = true;
            }

            return changed;
        }

        private static bool IsSceneGroup(string groupName)
        {
            return groupName == LegacyGroupName || groupName.StartsWith(GroupPrefix, StringComparison.Ordinal);
        }
    }
}