using UnityEditor;

namespace Internal
{
    // A scene added, deleted, renamed or moved in or out of a scene folder changes the Scenes catalogue and the
    // Addressables entries of the scenes.
    public sealed class SceneCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsScene(importedAssets) ||
                ContainsScene(deletedAssets) ||
                ContainsScene(movedAssets) ||
                ContainsScene(movedFromAssetPaths))
                SceneGenerator.ScheduleGenerate();
        }

        private static bool ContainsScene(string[] paths)
        {
            foreach (var path in paths)
            {
                if (SceneGenerator.IsSceneInput(path))
                    return true;
            }

            return false;
        }
    }
}
