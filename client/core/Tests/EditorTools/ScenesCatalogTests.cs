using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Internal.EditorTests
{
    public class ScenesCatalogTests
    {
        [Test]
        public void ScenesWithTheSameName_CompileOrAreRefused()
        {
            AssertCompilesOrRefused(new List<(string, string)> { ("Main", "guid1"), ("Main", "guid2") }, "Main");
        }

        [Test]
        public void SceneNamedScenes_CompilesOrIsRefused()
        {
            AssertCompilesOrRefused(new List<(string, string)> { ("Scenes", "guid1") }, "Scenes");
        }

        // 1_Intro.unity: the identifier gets a prefix instead of starting with a digit.
        [Test]
        public void SceneStartingWithDigit_Compiles()
        {
            var source = ScenesClassGenerator.Build(new List<(string, string)> { ("1Intro", "guid1") });

            Assert.That(GeneratedCodeCompiler.Compile(source), Is.Empty, "Compile errors");
        }

        // The order of the members must not depend on the culture of the machine that generated the file.
        [Test]
        public void Scenes_AreOrderedOrdinally()
        {
            var source = ScenesClassGenerator.Build(new List<(string, string)> { ("Aa", "guid1"), ("AB", "guid2") });

            Assert.That(source.IndexOf(" AB ", StringComparison.Ordinal),
                Is.LessThan(source.IndexOf(" Aa ", StringComparison.Ordinal)));
        }

        // Deleting the last scene must empty the catalogue, not leave members of scenes that are gone.
        [Test]
        public void NoScenesLeft_CatalogueIsWrittenEmpty()
        {
            List<(string sceneName, string sceneGuid)> written = null;

            SceneGenerator.Generate(new[] { EditorTestPaths.Root }, scenes => written = scenes);

            Assert.That(written, Is.Not.Null.And.Empty);
        }

        // A scene added, moved or deleted reaches the catalogue without a domain reload.
        [Test]
        public void SceneDeleted_SchedulesCatalogue()
        {
            var postprocessor = TypeCache.GetTypesDerivedFrom<AssetPostprocessor>()
                .SingleOrDefault(type => type.Assembly == typeof(SceneGenerator).Assembly &&
                                         type.Name.StartsWith("Scene", StringComparison.Ordinal));

            Assert.That(postprocessor, Is.Not.Null, "The scenes catalogue has no postprocessor.");

            var none = Array.Empty<string>();
            var deleted = new[] { FrameworkSettings.SceneFolders[0] + "/Deleted.unity" };
            postprocessor.GetMethod("OnPostprocessAllAssets", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { none, deleted, none, none });

            var runner = typeof(SceneGenerator).GetField("Runner", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            var isScheduled = typeof(CatalogGenerationRunner)
                .GetField("_isScheduled", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(runner);

            Assert.That(isScheduled, Is.True);
        }

        private static void AssertCompilesOrRefused(List<(string, string)> scenes, string name)
        {
            string source;

            try
            {
                source = ScenesClassGenerator.Build(scenes);
            }
            catch (InvalidOperationException exception)
            {
                Assert.That(exception.Message, Does.Contain($"'{name}'"));
                return;
            }

            Assert.That(GeneratedCodeCompiler.Compile(source), Is.Empty, "Compile errors");
        }
    }
}
