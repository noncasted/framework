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

        // The end of the last started load: loads run one after another, each waits for the previous one.
        private UniTaskCompletionSource _previous;

        public async UniTask<ILoadedScope> Load(Func<IServiceScopeLoader, ILoadedScope, UniTask<ILoadedScope>> action)
        {
            var previous = _previous;
            var current = new UniTaskCompletionSource();
            _previous = current;

            try
            {
                // A load that overlaps another one would build its scope next to the other's, and the scope finished
                // first would never be disposed. The previous load completes it either way: its error went to its caller.
                if (previous != null)
                    await previous.Task;

                // Scopes share service scene names, so the previous one is fully unloaded
                // before the new one starts loading: otherwise SceneManager.CreateScene fails on a duplicate
                // name, and the scenes of the old scope stay hanging over the new one.
                await Unload();

                var currentScope = await action(_serviceScopeLoader, _parentScope);
                _currentScope = currentScope;
                return currentScope;
            }
            finally
            {
                current.TrySetResult();
            }
        }

        // The scope is forgotten before its dispose: a failed dispose is reported to this load once, the next load
        // does not inherit it.
        private UniTask Unload()
        {
            if (_currentScope == null)
                return UniTask.CompletedTask;

            var scope = _currentScope;
            _currentScope = null;

            return scope.Dispose();
        }
    }
}
