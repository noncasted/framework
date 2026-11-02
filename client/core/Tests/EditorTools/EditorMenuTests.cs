using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    public class EditorMenuTests
    {
        private const string Folder = "Assets/EditorMenuTests";
        private const string AssetPath = Folder + "/Clip.anim";

        [TearDown]
        public void TearDown()
        {
            Selection.objects = Array.Empty<Object>();
            EditorTestAssets.DeleteFolder(Folder);
        }

        // The first item of the context menu of any asset: right click on a prefab and a click destroys the prefab.
        [Test]
        public void DestroyNested_MainAsset_IsNotOffered_AndNotDestroyed()
        {
            CreateClipWithNested();

            var main = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            Selection.objects = new[] { main };

            var validate = FindMenu(true);
            Assert.IsNotNull(validate, "the item has no validation: it is offered for every asset");
            Assert.IsFalse((bool)validate.Invoke(null, null));

            Assert.Catch<Exception>(() => FindMenu(false).Invoke(null, null));
            Assert.IsTrue(AssetDatabase.LoadMainAssetAtPath(AssetPath) != null, "the asset is destroyed");
        }

        [Test]
        public void DestroyNested_NestedAsset_IsOffered_AndDestroyed()
        {
            CreateClipWithNested();

            var nested = AssetDatabase.LoadAllAssetsAtPath(AssetPath).First(AssetDatabase.IsSubAsset);
            Selection.objects = new[] { nested };

            var validate = FindMenu(true);
            Assert.IsNotNull(validate);
            Assert.IsTrue((bool)validate.Invoke(null, null));

            // The menu refreshes the database, which logs the problems of the whole project.
            LogAssert.ignoreFailingMessages = true;
            FindMenu(false).Invoke(null, null);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(AssetDatabase.LoadAllAssetsAtPath(AssetPath).Any(AssetDatabase.IsSubAsset));
            Assert.IsTrue(AssetDatabase.LoadMainAssetAtPath(AssetPath) != null);
        }

        // The validator tells to run this menu for a service on an inactive object: the menu has to see it.
        [Test]
        public void ScanServices_SeesInactiveObjects()
        {
            var root = new GameObject("root");
            var hidden = new GameObject("hidden");
            hidden.transform.SetParent(root.transform);

            try
            {
                var listener = hidden.AddComponent<ReloadListener>();
                hidden.SetActive(false);

                typeof(SceneServicesScanner)
                    .GetMethod("ScanServices", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);

                Assert.AreEqual(1, listener.Reloads);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A namespace that is not one: the generated file does not compile, the queue of the generator waits for good.
        [TestCase("My Game")]
        [TestCase("1Game")]
        public void BindingsWindow_BadNamespace_IsAProblem(string name)
        {
            var window = ScriptableObject.CreateInstance<HierarchyBindingsWindow>();

            try
            {
                typeof(HierarchyBindingsWindow)
                    .GetField("_namespace", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(window, name);

                var existing = typeof(HierarchyBindingsWindow)
                    .GetField("_existing", BindingFlags.Instance | BindingFlags.NonPublic);
                existing.SetValue(window, Activator.CreateInstance(existing.FieldType));

                var problem = (string)typeof(HierarchyBindingsWindow)
                                      .GetMethod("FindNameProblem", BindingFlags.Instance | BindingFlags.NonPublic)
                                      .Invoke(window, new object[] { "GoodName" });

                Assert.IsNotEmpty(problem);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private static void CreateClipWithNested()
        {
            AssetDatabase.CreateFolder("Assets", "EditorMenuTests");
            AssetDatabase.CreateAsset(new AnimationClip(), AssetPath);
            AssetDatabase.AddObjectToAsset(new AnimationClip { name = "Nested" }, AssetPath);
            AssetDatabase.SaveAssets();
        }

        private static MethodInfo FindMenu(bool validate)
        {
            return typeof(ScriptableObjectsDestroyer)
                   .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                   .FirstOrDefault(method => method.GetCustomAttributes<MenuItem>().Any(item => item.validate == validate));
        }

        private class ReloadListener : MonoBehaviour, ISceneReloadListener
        {
            public int Reloads { get; private set; }

            public bool OnReload()
            {
                Reloads++;
                return false;
            }
        }
    }
}
