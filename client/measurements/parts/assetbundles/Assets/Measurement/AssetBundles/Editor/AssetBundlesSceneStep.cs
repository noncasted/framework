using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Measurement
{
    /// <summary>
    ///     Adds <see cref="AssetBundlesPart" />: a prefab and an additive scene with the sprite of the measurement, and three
    ///     AssetBundles built into StreamingAssets/Bundles before the player (the sprite apart, so the prefab and the scene
    ///     depend on its bundle).
    /// </summary>
    public sealed class AssetBundlesSceneStep : IMeasurementSceneStep
    {
        private const string Output = "Assets/StreamingAssets/" + AssetBundlesPart.Folder;

        public void Build(MeasurementSceneContext context)
        {
            var sprite = AssetDatabase.GetAssetPath(context.Assets.Sprite);
            var prefab = CreatePrefab(context);
            var scene = CreateScene(context);

            // The part is added and the scene saved before the bundles: BuildAssetBundles reloads the open scenes
            // from disk, so an object made before it and not saved is lost.
            var part = new GameObject("AssetBundles").AddComponent<AssetBundlesPart>();
            context.Run.AddPart(part);
            EditorSceneManager.SaveOpenScenes();

            if (Directory.Exists(Output))
                Directory.Delete(Output, true);

            Directory.CreateDirectory(Output);

            var builds = new[]
            {
                new AssetBundleBuild { assetBundleName = AssetBundlesPart.Shared, assetNames = new[] { sprite } },
                new AssetBundleBuild { assetBundleName = AssetBundlesPart.Prefab, assetNames = new[] { prefab } },
                new AssetBundleBuild { assetBundleName = AssetBundlesPart.Scene, assetNames = new[] { scene } }
            };

            // Without type trees, as ContentBuild of the framework writes the bundles of a game: a bundle with type trees
            // makes the player generate the type trees of the loaded types (GenerateTypeTreeTransfer) to compare them.
            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableWriteTypeTree;
            var manifest = BuildPipeline.BuildAssetBundles(Output, builds, options, BuildTarget.WebGL);

            if (manifest == null)
                throw new InvalidOperationException($"The asset bundles were not built into {Output}");

            var dependencies = string.Join(", ", manifest.GetAllDependencies(AssetBundlesPart.Prefab));
            Debug.Log($"[measurement] asset bundles {string.Join(", ", manifest.GetAllAssetBundles())}, prefab depends on {dependencies}");
            AssetDatabase.Refresh();

        }

        private static string CreatePrefab(MeasurementSceneContext context)
        {
            var path = $"{context.Folder}/{AssetBundlesPart.PrefabAsset}.prefab";
            var item = new GameObject(AssetBundlesPart.PrefabAsset);
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
            var path = $"{context.Folder}/AssetBundleScene.unity";
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var item = new GameObject(AssetBundlesPart.SceneObject);
            SceneManager.MoveGameObjectToScene(item, scene);
            item.AddComponent<SpriteRenderer>().sprite = context.Assets.Sprite;
            item.transform.position = new Vector3(0f, 3f, 0f);

            if (EditorSceneManager.SaveScene(scene, path) == false)
                throw new InvalidOperationException($"The scene was not saved to {path}");

            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(active);
            return path;
        }
    }
}
