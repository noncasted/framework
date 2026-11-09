using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Internal
{
    // Play Mode of the editor: no directory is built or registered. Without a registered directory Loadable.Load /
    // LoadAsync and SceneManager.LoadSceneAsync(LoadableSceneId) give the assets and scenes of the project
    // (AssetDatabase), and Release does not destroy them; the roots are the root assets of the project. Nothing to fetch.
    internal sealed class EditorContentDirectory : IContentDirectory
    {
        public EditorContentDirectory(string name, IReadOnlyList<Object> roots)
        {
            if (roots.Count == 0)
                throw new ArgumentException($"[Content] The content directory {name} has no roots", nameof(roots));

            Name = name;
            _roots = roots;
        }

        private readonly IReadOnlyList<Object> _roots;

        public string Name { get; }

        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            return _roots.OfType<T>().ToArray();
        }

        public UniTask FetchObject(IReadOnlyLifetime lifetime, string guid, long localId, IProgress<long> progress)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public UniTask FetchScene(IReadOnlyLifetime lifetime, string guid, IProgress<long> progress)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }
    }
}
