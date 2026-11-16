using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Internal
{
    // Scope scenes: everything loaded or created here is unloaded together with the scope.
    public class ServiceScopeSceneLoader
    {
        public ServiceScopeSceneLoader(IReadOnlyLifetime lifetime, ScopeContent content)
        {
            _lifetime = lifetime;
            _content = content;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly ScopeContent _content;
        private readonly List<ILoadedScene> _results = new();

        public IReadOnlyList<ILoadedScene> Results => _results;

        // A scene of a content bundle, loaded on the lifetime of the scope; the dispose of the scope waits for its unload.
        public async UniTask<ILoadedScene> Load(ContentScene scene, bool isMain = false)
        {
            var result = await _content.Loader.LoadScene(_lifetime, scene, isMain);
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