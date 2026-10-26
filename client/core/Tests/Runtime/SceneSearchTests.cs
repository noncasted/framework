using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    // The search of a typed object in a loaded scene (SceneLoaderExtensions); scenes are created in play mode.
    public class SceneSearchTests
    {
        [Test]
        public void GetRoot_Missing_ThrowsInvalidOperation()
        {
            var scene = SceneManager.CreateScene("GetRoot_Missing");

            try
            {
                Assert.Throws<InvalidOperationException>(() => scene.GetRoot<TestBehaviour>());
            }
            finally
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [Test]
        public void FindInOpenScene_ObjectOfAnotherSceneOnly_Throws()
        {
            var scene = SceneManager.CreateScene("FindInOpenScene_Other");
            var other = new GameObject("Other").AddComponent<TestBehaviour>();
            SceneManager.MoveGameObjectToScene(other.gameObject, scene);

            try
            {
                Assert.AreSame(other, SceneLoaderExtensions.FindInOpenScene<TestBehaviour>("FindInOpenScene_Other"));
                Assert.Throws<InvalidOperationException>(() =>
                    SceneLoaderExtensions.FindInOpenScene<TestBehaviour>("FindInOpenScene_Mock"));
            }
            finally
            {
                Object.Destroy(other.gameObject);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
