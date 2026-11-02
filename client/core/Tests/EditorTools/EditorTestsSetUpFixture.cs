using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Internal.EditorTests
{
    // The assets of these tests schedule the catalogue generators (debounced 0.5 s), and a generator logs an error in a
    // project without addressables. Here it fires between the tests, all synchronous, but the assemblies after this one
    // have tests that wait over frames: an error logged inside one fails it. What is scheduled ends with the assembly.
    [SetUpFixture]
    public class EditorTestsSetUpFixture
    {
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
        }

        private static EditorApplication.CallbackFunction[] Callbacks(EditorApplication.CallbackFunction list)
        {
            return list == null
                ? Array.Empty<EditorApplication.CallbackFunction>()
                : Array.ConvertAll(list.GetInvocationList(), item => (EditorApplication.CallbackFunction)item);
        }
    }
}
