using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    public class PrefabScreenshotTests
    {
        private const string PrefabPath = "Assets/PrefabScreenshotTests.prefab";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        [Test]
        public void MissingPrefab_IsAnError()
        {
            Assert.AreEqual("ERROR: Prefab not found at 'Assets/Nothing.prefab'", PrefabScreenshot.Capture("Assets/Nothing.prefab"));
        }

        [Test]
        public void Volume_PrintsTheHierarchyAndWritesTheImage()
        {
            SavePrefab(root => {
                root.AddComponent<BoxCollider>();

                var child = new GameObject("Child");
                child.transform.SetParent(root.transform);
                child.AddComponent<Light>();
                child.SetActive(false);
            });

            var lines = Lines(Capture());

            Assert.AreEqual("OK", lines[0], string.Join("\n", lines));
            StringAssert.StartsWith("IMAGE_PATH:", lines[1]);
            StringAssert.EndsWith(Path.Combine("Temp", "PrefabScreenshots", "PrefabScreenshotTests.png"), lines[1]);
            FileAssert.Exists(lines[1].Substring("IMAGE_PATH:".Length));
            Assert.AreEqual($"PREFAB:{PrefabPath}", lines[2]);
            Assert.AreEqual("IS_UI:False", lines[3]);
            Assert.AreEqual("RESOLUTION:32x32", lines[4]);
            Assert.AreEqual("---HIERARCHY---", lines[5]);
            Assert.AreEqual("- PrefabScreenshotTests", lines[6]);
            Assert.AreEqual("  [BoxCollider]", lines[7]);
            Assert.AreEqual("  - Child [INACTIVE]", lines[8]);
            Assert.AreEqual("    [Light]", lines[9]);
        }

        [Test]
        public void Flat_IsCaptured()
        {
            SavePrefab(root => root.AddComponent<SpriteRenderer>());

            var lines = Lines(Capture());

            Assert.AreEqual("OK", lines[0], string.Join("\n", lines));
            Assert.AreEqual("IS_UI:False", lines[3]);
        }

        [Test]
        public void Ui_IsCapturedInACanvas_AndPrintsTheFields()
        {
            SavePrefab(root => {
                root.AddComponent<RectTransform>();
                root.AddComponent(Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true));
            });

            var result = Capture();
            var lines = Lines(result);

            Assert.AreEqual("OK", lines[0], result);
            Assert.AreEqual("IS_UI:True", lines[3]);
            StringAssert.Contains("[CanvasRenderer]", result);
            StringAssert.Contains("[Image] (", result);
            StringAssert.Contains("m_Material=None", result);
            Assert.IsNull(GameObject.Find("_ScreenshotCanvas"));
            Assert.IsNull(GameObject.Find("_ScreenshotCamera"));
        }

        private static string Capture()
        {
            return PrefabScreenshot.Capture(PrefabPath, 32);
        }

        private static void SavePrefab(Action<GameObject> build)
        {
            var root = new GameObject("PrefabScreenshotTests");

            try
            {
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static string[] Lines(string text)
        {
            return text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        }
    }
}
