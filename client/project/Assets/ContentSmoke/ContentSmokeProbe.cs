using System;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContentSmoke
{
    // The only scene of a player build of client/project: registers the content directory of the build over the store
    // of the platform (fetched on demand on WebGL, StreamingAssets elsewhere), fetches and loads the target of its root
    // and its scene, logs each step. A standalone player quits after it; a page stays (the smoke reopens it).
    public sealed class ContentSmokeProbe : MonoBehaviour
    {
        private const string Directory = "Smoke";

        private readonly Lifetime _lifetime = new();

        private void Start()
        {
            Run().Forget();
        }

        private void OnDestroy()
        {
            _lifetime.Terminate();
        }

        private async UniTaskVoid Run()
        {
            try
            {
                var json = Resources.Load<TextAsset>(ContentPaths.IndexResource(Directory));
                var index = ContentIndex.FromJson(json.text);
                Debug.Log($"[ContentSmoke] index {index.Name}: {index.Files.Count} files, root files {index.RootFiles.Count}, preloaded {index.IsPreloaded}");

                var store = ContentStores.Create(index, null);
                var directory = await ContentDirectory.Register(_lifetime, index, store);
                var root = directory.RootAssets<ContentSmokeRoot>().Single();
                Debug.Log($"[ContentSmoke] registered {store.GetType().Name} at {store.LocalPath}: root {root.name}");

                // A smoke only: the engine does not promise the format of the id.
                var guid = Regex.Match(root.Target.LoadableObjectId.ToString(), "guid: ([0-9a-f]{32})").Groups[1].Value;
                await directory.FetchObject(_lifetime, guid, 0, null);
                var target = root.Target.Load();
                Debug.Log($"[ContentSmoke] loaded '{target.Text}', status {root.Target.Status}");

                await directory.FetchScene(_lifetime, root.Scene.ToString(), null);
                await SceneManager.LoadSceneAsync(root.Scene, new LoadSceneParameters(LoadSceneMode.Additive));
                Debug.Log($"[ContentSmoke] scene loaded: {SceneManager.GetSceneByLoadableSceneId(root.Scene).name}");
                Debug.Log("[ContentSmoke] done");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.Log("[ContentSmoke] failed");
            }
#if !UNITY_WEBGL
            Application.Quit(0);
#endif
        }
    }
}
