using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Measurement
{
    /// <summary>
    ///     What the games load through Addressables: the catalog of StreamingAssets, a prefab with a sprite from a
    ///     bundle and an additive scene of a bundle (as the scene loader of the core does).
    /// </summary>
    public sealed class AddressablesPart : MonoBehaviour, IMeasurementPart
    {
        public const string PrefabKey = "Measurement/Prefab";
        public const string SceneKey = "Measurement/Scene";
        public const string SceneObject = "Addressable Scene Object";

        public string Name => "addressables";

        public async UniTask<string> Run(CancellationToken cancellation)
        {
            // Not released by itself, so its status is still readable when it is done.
            var initialization = Addressables.InitializeAsync(false);
            await Wait(initialization, cancellation);
            initialization.Release();

            var prefab = await Wait(Addressables.LoadAssetAsync<GameObject>(PrefabKey), cancellation);
            var instance = Instantiate(prefab, transform);
            var sprite = instance.GetComponent<SpriteRenderer>().sprite;

            if (sprite == null)
                throw new InvalidOperationException($"The prefab {PrefabKey} came without its sprite");

            var scene = await Wait(Addressables.LoadSceneAsync(SceneKey, LoadSceneMode.Additive), cancellation);

            if (GameObject.Find(SceneObject) == null)
                throw new InvalidOperationException($"The scene {SceneKey} has no {SceneObject}");

            return $"prefab with sprite {sprite.texture.width} px, scene {scene.Scene.name} with {scene.Scene.rootCount} roots";
        }

        private static async UniTask<T> Wait<T>(AsyncOperationHandle<T> handle, CancellationToken cancellation)
        {
            await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: cancellation);

            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"Addressables operation {handle.DebugName} failed", handle.OperationException);

            return handle.Result;
        }
    }
}
