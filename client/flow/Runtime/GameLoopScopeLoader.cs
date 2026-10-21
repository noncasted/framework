using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IGameLoopScopeLoader
    {
        UniTask<ILoadedScope> Load(Func<IServiceScopeLoader, ILoadedScope, UniTask<ILoadedScope>> action);
    }

    // One scope of the game loop at a time (menu, match, ...): loading the next one unloads the previous one first.
    public class GameLoopScopeLoader : IGameLoopScopeLoader
    {
        public GameLoopScopeLoader(
            IServiceScopeLoader serviceScopeLoader,
            ILoadedScope parentScope)
        {
            _serviceScopeLoader = serviceScopeLoader;
            _parentScope = parentScope;
        }

        private readonly IServiceScopeLoader _serviceScopeLoader;
        private readonly ILoadedScope _parentScope;

        private ILoadedScope _currentScope;
        private UniTask _unloading;

        public async UniTask<ILoadedScope> Load(Func<IServiceScopeLoader, ILoadedScope, UniTask<ILoadedScope>> action)
        {
            // Scopes share service scene names, so the previous one is fully unloaded
            // before the new one starts loading: otherwise SceneManager.CreateScene fails on a duplicate
            // name, and the scenes of the old scope stay hanging over the new one.
            await Unload();

            var currentScope = await action(_serviceScopeLoader, _parentScope);
            _currentScope = currentScope;
            return currentScope;
        }

        private UniTask Unload()
        {
            if (_currentScope == null)
                return _unloading;

            var scope = _currentScope;
            _currentScope = null;
            _unloading = scope.Dispose();

            return _unloading;
        }
    }
}
