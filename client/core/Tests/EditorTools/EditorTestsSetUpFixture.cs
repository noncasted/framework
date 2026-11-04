using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Internal.EditorTests
{
    // The assets of these tests schedule the catalogue generators (debounced 0.5 s): a scheduled run is paused for the
    // assembly (a generation of the catalogues of the project between two tests cost 5-6 s of every run). What is
    // scheduled ends with the assembly too: the assemblies after this one have tests that wait over frames, and a
    // generator logs an error in a project without addressables, which fails such a test.
    [SetUpFixture]
    public class EditorTestsSetUpFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            CatalogGenerationRunner.PauseScheduled();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            var isScheduled = typeof(CatalogGenerationRunner)
                .GetField("_isScheduled", BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var callback in Callbacks(EditorApplication.update))
            {
                if (callback.Target is CatalogGenerationRunner runner)
                {
                    EditorApplication.update -= callback;
                    isScheduled.SetValue(runner, false);
                }
            }

            foreach (var callback in Callbacks(EditorApplication.delayCall))
            {
                if (callback.Target is CatalogGenerationRunner)
                    EditorApplication.delayCall -= callback;
            }

            CatalogGenerationRunner.ResumeScheduled();
        }

        private static EditorApplication.CallbackFunction[] Callbacks(EditorApplication.CallbackFunction list)
        {
            return list == null
                ? Array.Empty<EditorApplication.CallbackFunction>()
                : Array.ConvertAll(list.GetInvocationList(), item => (EditorApplication.CallbackFunction)item);
        }
    }
}
