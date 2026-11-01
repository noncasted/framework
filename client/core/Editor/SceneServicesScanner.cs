using UnityEditor;
using UnityEngine.SceneManagement;

namespace Internal
{
    public static class SceneServicesScanner
    {
        [MenuItem("Assets/Scan services %e", priority = -1000)]
        private static void ScanServices()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (scene.isLoaded)
                    HierarchyBindingsEntityViews.SyncSceneServices(scene);
            }
        }
    }
}
