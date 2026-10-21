using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Internal
{
    // Scope scenes: everything loaded or created here is unloaded together with the scope.
    public class ServiceScopeSceneLoader
    {
        private readonly List<ILoadedScene> _results = new();

        public IReadOnlyList<ILoadedScene> Results => _results;

        public async UniTask<ILoadedScene> Load(AssetReference scene, bool isMain = false)
        {
            var handle = await Addressables.LoadSceneAsync(scene, LoadSceneMode.Additive).ToUniTask();

            if (isMain == true)
                SceneManager.SetActiveScene(handle.Scene);

            var result = new LoadedScene(handle);
            _results.Add(result);

            return result;
        }

        /// <summary>
        /// An empty container for the scope objects: there is no need to load a scene asset for it,
        /// it is unloaded together with the other scenes of the scope.
        /// </summary>
        public ILoadedScene Create(string name)
        {
            var scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.None));
            var result = new CreatedScene(scene);

            _results.Add(result);

            return result;
        }
    }
}