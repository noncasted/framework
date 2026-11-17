using System;
using System.Linq;
using UnityEditor.SceneManagement;

namespace Internal.EditorTests
{
    // A new empty scene for the content build of a test, whatever the tests before it left open (the Scriptable Build
    // Pipeline builds nothing while an open scene has unsaved changes), and the scenes of before restored after it from
    // their files; their unsaved changes are lost, an untitled scene is replaced by a new empty one.
    public sealed class ContentTestScenes : IDisposable
    {
        private readonly SceneSetup[] _setup;

        private ContentTestScenes(SceneSetup[] setup)
        {
            _setup = setup;
        }

        public static ContentTestScenes Clean()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return new ContentTestScenes(setup);
        }

        public void Dispose()
        {
            if (_setup.Length > 0 && _setup.All(scene => string.IsNullOrEmpty(scene.path) == false))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
