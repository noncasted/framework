using System.Linq;
using UnityEditor.Build;
using UnityEngine.SceneManagement;

namespace Internal
{
    // The Scriptable Build Pipeline builds no bundle while an open scene has unsaved changes (ReturnCode.UnsavedChanges):
    // the content build checks it first and names the scenes, so the reason is in the error and not in a return code.
    public static class ContentBuildScenes
    {
        public static void ThrowIfDirty()
        {
            var dirty = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.isDirty)
                .Select(scene => string.IsNullOrEmpty(scene.path) ? $"{(string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name)} (never saved)" : scene.path)
                .ToArray();

            if (dirty.Length == 0)
                return;

            throw new BuildFailedException(
                $"[ContentBuild] The open scenes {string.Join(", ", dirty)} have unsaved changes: the Scriptable Build Pipeline builds no " +
                "content bundle then (UnsavedChanges). Save or discard them (File > Save, or open another scene) and build again; " +
                "a tool or a test that builds opens a new scene first (EditorSceneManager.NewScene) and restores its scenes after");
        }
    }
}
