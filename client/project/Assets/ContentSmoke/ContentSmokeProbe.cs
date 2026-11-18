using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace ContentSmoke
{
    // The only scene of a player build of client/project: registers the content bundle Smoke of the build over the store
    // of the platform (fetched on WebGL, StreamingAssets elsewhere), loads the target of its root and its scene through
    // IContentLoader, logs each step. A standalone player quits after it; a page stays (the smoke reopens it).
    public sealed class ContentSmokeProbe : MonoBehaviour
    {
        private const string Bundle = "Smoke";

        private readonly Lifetime _lifetime = new();

        private void Start()
        {
            Run().NoAwait();
        }

        private void OnDestroy()
        {
            _lifetime.Terminate();
        }

        // The top of a task nobody awaits: the failure is logged for the smoke script.
        private async UniTask Run()
        {
            try
            {
                var loader = await ContentEntryPoint.RegisterMain(_lifetime, new ContentOptions(Bundle));
                var root = loader.RootAssets<ContentSmokeRoot>().Single();
                Debug.Log($"[ContentSmoke] registered {loader.Name}: root {root.name}, target {root.Target}");

                IContentLoader content = loader;
                var target = await content.Load(_lifetime, root.Target);
                Debug.Log($"[ContentSmoke] loaded '{target.Text}', loaded {root.Target.IsLoaded}");

                var scene = await content.LoadScene(_lifetime, root.Scene);
                Debug.Log($"[ContentSmoke] scene loaded: {scene.Scene.name}");
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
