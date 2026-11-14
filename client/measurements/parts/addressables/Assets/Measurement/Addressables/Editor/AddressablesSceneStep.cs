using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Measurement
{
    /// <summary>
    ///     Adds <see cref="AddressablesPart" />: the settings of Addressables in the generated folder (made again on
    ///     every run), one group of bundles with a prefab and a scene, built with the player into StreamingAssets.
    /// </summary>
    public sealed class AddressablesSceneStep : IMeasurementSceneStep
    {
        public void Build(MeasurementSceneContext context)
        {
            var settings = AddressableAssetSettings.Create($"{context.Folder}/AddressableAssetsData", "AddressableAssetSettings", true, true);
            AddressableAssetSettingsDefaultObject.Settings = settings;
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;

            var group = settings.CreateGroup("Measurement", false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            AddEntry(settings, group, CreatePrefab(context), AddressablesPart.PrefabKey);
            AddEntry(settings, group, CreateScene(context), AddressablesPart.SceneKey);

            var part = new GameObject("Addressables").AddComponent<AddressablesPart>();
            context.Run.AddPart(part);
        }

        private static string CreatePrefab(MeasurementSceneContext context)
        {
            var path = $"{context.Folder}/AddressablePrefab.prefab";
            var item = new GameObject("Addressable Prefab");
            item.AddComponent<SpriteRenderer>().sprite = context.Assets.Sprite;
            PrefabUtility.SaveAsPrefabAsset(item, path, out var saved);
            UnityEngine.Object.DestroyImmediate(item);

            if (saved == false)
                throw new InvalidOperationException($"The prefab was not saved to {path}");

            return path;
        }

        // An additive scene beside the measured one, closed after saving so the steps go on in the measured scene.
        private static string CreateScene(MeasurementSceneContext context)
        {
            var path = $"{context.Folder}/AddressableScene.unity";
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var item = new GameObject(AddressablesPart.SceneObject);
            SceneManager.MoveGameObjectToScene(item, scene);
            item.AddComponent<SpriteRenderer>().sprite = context.Assets.Sprite;
            item.transform.position = new Vector3(0f, 3f, 0f);

            if (EditorSceneManager.SaveScene(scene, path) == false)
                throw new InvalidOperationException($"The scene was not saved to {path}");

            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(active);
            return path;
        }

        private static void AddEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);

            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException($"No asset at {path}");

            settings.CreateOrMoveEntry(guid, group).address = address;
        }
    }
}
