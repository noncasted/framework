using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Measurement
{
    /// <summary>
    ///     Own bundles without Addressables: three AssetBundles of StreamingAssets (a shared sprite, a prefab and a scene that
    ///     depend on it), fetched by the page into the file system (AssetBundlesPart.jslib, no UnityWebRequest) and loaded by
    ///     AssetBundle.LoadFromFileAsync in the order of their dependencies.
    /// </summary>
    public sealed class AssetBundlesPart : MonoBehaviour, IMeasurementPart
    {
        public const string Folder = "Bundles";
        public const string Shared = "shared";
        public const string Prefab = "prefab";
        public const string Scene = "scene";
        public const string PrefabAsset = "AssetBundlePrefab";
        public const string SceneObject = "Asset Bundle Scene Object";

        public string Name => "assetbundles";

        public async UniTask<string> Run(CancellationToken cancellation)
        {
            var shared = await Open(Shared, cancellation);
            var prefabs = await Open(Prefab, cancellation);
            var scenes = await Open(Scene, cancellation);

            var request = prefabs.LoadAssetAsync<GameObject>(PrefabAsset);
            await UniTask.WaitUntil(() => request.isDone, cancellationToken: cancellation);
            var prefab = (GameObject)request.asset ?? throw new InvalidOperationException($"No {PrefabAsset} in the bundle {Prefab}");
            var sprite = Instantiate(prefab, transform).GetComponent<SpriteRenderer>().sprite;

            if (sprite == null)
                throw new InvalidOperationException($"The prefab {PrefabAsset} came without its sprite from the bundle {Shared}");

            var scenePath = scenes.GetAllScenePaths()[0];
            var load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            await UniTask.WaitUntil(() => load.isDone, cancellationToken: cancellation);

            if (GameObject.Find(SceneObject) == null)
                throw new InvalidOperationException($"The scene {scenePath} has no {SceneObject}");

            return $"bundles {shared.name}, {prefabs.name}, {scenes.name}: prefab with sprite {sprite.texture.width} px, scene {Path.GetFileNameWithoutExtension(scenePath)}";
        }

        private static async UniTask<AssetBundle> Open(string name, CancellationToken cancellation)
        {
            var path = await Fetch(name, cancellation);
            var request = AssetBundle.LoadFromFileAsync(path);
            await UniTask.WaitUntil(() => request.isDone, cancellationToken: cancellation);
            return request.assetBundle ?? throw new InvalidOperationException($"The bundle {name} did not load from {path}");
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static async UniTask<string> Fetch(string name, CancellationToken cancellation)
        {
            var folder = Path.Combine(Application.temporaryCachePath, Folder);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, name);
            var fetch = MeasurementBundle_Fetch($"{Application.streamingAssetsPath}/{Folder}/{name}", path);

            while (true)
            {
                var status = MeasurementBundle_Status(fetch);

                if (status == 1)
                    return path;

                if (status == 2)
                    throw new IOException($"Fetching the bundle {name} failed (the reason is in the console)");

                await UniTask.Yield(cancellation);
            }
        }

        [DllImport("__Internal")]
        private static extern int MeasurementBundle_Fetch(string url, string path);

        [DllImport("__Internal")]
        private static extern int MeasurementBundle_Status(int fetch);
#else
        private static UniTask<string> Fetch(string name, CancellationToken cancellation)
        {
            return UniTask.FromResult(Path.Combine(Application.streamingAssetsPath, Folder, name));
        }
#endif
    }
}
