using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class SceneLoaderExtensions
    {
        public static async UniTask<T> LoadTyped<T>(this ServiceScopeSceneLoader loader, AssetReference scene, bool isMain = false)
        {
            var result = await loader.Load(scene, isMain);
            return result.Scene.GetRoot<T>();
        }

        public static async UniTask<T> LoadTyped<T>(this ServiceScopeSceneLoader loader, ContentScene scene, bool isMain = false)
        {
            var result = await loader.Load(scene, isMain);
            return result.Scene.GetRoot<T>();
        }

        // The component of the type on a root object of the scene.
        public static T GetRoot<T>(this Scene scene)
        {
            var rootObjects = scene.GetRootGameObjects();

            foreach (var rootObject in rootObjects)
            {
                if (rootObject.TryGetComponent(out T searched) == true)
                    return searched;
            }

            // The scene stays in the results of the loader: the failed scope unloads it with its other scenes.
            throw new InvalidOperationException($"{typeof(T)} is not on a root object of the scene {scene.name}");
        }

        public static async UniTask<T> FindOrLoadScene<T>(
            this IScopeBuilder utils,
            AssetReference scene,
            bool isMain = false)
            where T : MonoBehaviour
        {
#if UNITY_EDITOR
            if (utils.IsMock != true || SceneManager.GetSceneByName(scene.editorAsset.name).IsValid() != true)
                return await utils.SceneLoader.LoadTyped<T>(scene, isMain);

            return FindInOpenScene<T>(scene.editorAsset.name);
#else
            return await utils.SceneLoader.LoadTyped<T>(scene, isMain);
#endif
        }

        public static async UniTask<T> FindOrLoadScene<T>(
            this IScopeBuilder utils,
            ContentScene scene,
            bool isMain = false)
            where T : MonoBehaviour
        {
#if UNITY_EDITOR
            var open = SceneManager.GetSceneByPath(scene.EditorPath);

            if (utils.IsMock != true || open.IsValid() != true)
                return await utils.SceneLoader.LoadTyped<T>(scene, isMain);

            return FindInOpenScene<T>(open.name);
#else
            return await utils.SceneLoader.LoadTyped<T>(scene, isMain);
#endif
        }

#if UNITY_EDITOR
        // A mock started from an opened scene takes the object of that scene instead of loading it again.
        public static T FindInOpenScene<T>(string sceneName) where T : MonoBehaviour
        {
            var targets = Object.FindObjectsByType<T>(FindObjectsInactive.Include);

            foreach (var target in targets)
            {
                if (target.gameObject.scene.name != sceneName)
                    continue;

                return target;
            }

            // An object of another scene is not of this one: the scene of the mock lacks it.
            throw new InvalidOperationException($"{typeof(T)} is not in the opened scene {sceneName}");
        }
#endif
    }
}
