using System.Linq;
using Internal;
using Unity.Loading;
using UnityEngine;

namespace ContentSmoke
{
    // The only scene of a player build of client/project: logs what the player got from the content build (the index
    // from Resources, the directory in StreamingAssets) and how its lazy references look at runtime, then quits.
    public sealed class ContentSmokeProbe : MonoBehaviour
    {
        private const string Directory = "Smoke";

        // The same target as the root, baked into the scene of the player.
        [SerializeField] private Loadable<ContentSmokeTarget> _target;

        private void Start()
        {
            var json = Resources.Load<TextAsset>(ContentPaths.IndexResource(Directory));
            var index = ContentIndex.FromJson(json.text);
            Debug.Log($"[ContentSmoke] index {index.Name}: {index.Files.Count} files, root files {index.RootFiles.Count}, " +
                      $"objects {string.Join(", ", index.Objects.Select(entry => $"{entry.Guid}:{entry.LocalId}"))}, " +
                      $"scenes {string.Join(", ", index.Scenes.Select(entry => $"{entry.Guid} {entry.Path}"))}");
            Debug.Log($"[ContentSmoke] scene field: {_target.LoadableObjectId}");

            var handle = ContentLoadManager.RegisterContentDirectory($"{Application.streamingAssetsPath}/{ContentPaths.Directory(Directory)}");
            var root = ContentLoadManager.GetRootAssets<ContentSmokeRoot>(handle).Single();
            var target = root.Target.Load();

            Debug.Log($"[ContentSmoke] root field: {root.Target.LoadableObjectId}, scene id {root.Scene}");
            Debug.Log($"[ContentSmoke] loaded '{target.Text}', status {root.Target.Status}");
            Application.Quit(0);
        }
    }
}
