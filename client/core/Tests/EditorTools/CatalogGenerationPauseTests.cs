using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.EditorTests
{
    // The temporary assets of these tests schedule the catalogue generators, and a generation of the catalogues of the
    // project between two tests cost 5-6 s of every run: the scheduled runs are paused for the assembly.
    public class CatalogGenerationPauseTests
    {
        private const string Folder = "Assets/CatalogGenerationPauseTests";

        [UnityTest]
        public IEnumerator DeletedPrefab_GeneratesNothing()
        {
            var messages = new List<string>();
            Application.LogCallback listener = (message, _, _) => messages.Add(message);
            Application.logMessageReceived += listener;

            try
            {
                AssetDatabase.CreateFolder("Assets", "CatalogGenerationPauseTests");
                var probe = new GameObject("Probe");
                PrefabUtility.SaveAsPrefabAsset(probe, Folder + "/Probe.prefab", out _);
                Object.DestroyImmediate(probe);
                AssetDatabase.DeleteAsset(Folder);

                // One and a half debounces of CatalogGenerationRunner.
                var until = EditorApplication.timeSinceStartup + 0.75;
                while (EditorApplication.timeSinceStartup < until)
                    yield return null;
            }
            finally
            {
                Application.logMessageReceived -= listener;
                EditorTestAssets.DeleteFolder(Folder);
            }

            Assert.That(messages.FindAll(message =>
                    message.Contains("[PrefabCatalogGenerator]") || message.Contains("[ContainerGraphPostprocessor]")),
                Is.Empty);
        }
    }
}
