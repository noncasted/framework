using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    // Editor tools report a failure as a failure: a status, a log, not a silently smaller result.
    public class EditorToolFailureTests
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // A button shows its status right after the call: Generate catches and logs a failure, so the status would say
        // "generated". Wiring only: a real run writes the catalogues of the project.
        [TestCase("OnGeneratePrefabsClicked", typeof(PrefabCatalogGenerator))]
        [TestCase("OnGenerateSpritesClicked", typeof(SpriteGenerator))]
        [TestCase("OnGenerateAudioClicked", typeof(AudioCatalogGenerator))]
        public void AssetsTab_Button_CallsGeneratorThatThrows(string button, Type generator)
        {
            Assert.IsTrue(References(typeof(AssetsTab), button, generator.GetMethod("GenerateNow", Flags)));
        }

        // The group popup of the inspector reads the cache of the registry: art that arrived without the inspector (a pull,
        // a copied file) reaches the popup only through the generator. Wiring only: a real run writes a script, and the
        // compilation it requests stops the updates the later tests wait for.
        [TestCase(typeof(SpriteGenerator))]
        [TestCase(typeof(PrefabCatalogGenerator))]
        [TestCase(typeof(AssetCatalogGenerator))]
        [TestCase(typeof(AudioCatalogGenerator))]
        public void Generator_Run_DropsTheCacheOfTheGroups(Type generator)
        {
            Assert.IsTrue(References(generator, "GenerateInternal", typeof(CatalogGroupsRegistry).GetMethod("Invalidate")));
        }

        // Run All of the window and Regenerate All of the menu are the same run: colors, bindings, env assets, the asset
        // map and shared addressables are not skipped.
        [Test]
        public void AssetsTab_RunAll_IsRegenerateAll()
        {
            Assert.IsTrue(References(typeof(AssetsTab), "OnRunAllClicked", typeof(RegenerateAll).GetMethod("Run", Flags)));
        }

        [Test]
        public void Sprite_WithoutGroup_IsReported()
        {
            const string folder = "Assets/SpriteWithoutGroupTests";
            var settingsFile = Path.Combine(Path.GetDirectoryName(Application.dataPath), FrameworkSettings.SettingsPath);
            var settings = File.ReadAllText(settingsFile);
            var path = folder + "/Hero.png";

            try
            {
                File.WriteAllText(settingsFile, settings.Replace("\"ArtFolder\": \"\"", $"\"ArtFolder\": \"{folder}\""));

                // A crash of the editor in a test leaves the folder, and the next one would be "SpriteWithoutGroupTests 1".
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.CreateFolder("Assets", "SpriteWithoutGroupTests");

                var texture = new Texture2D(2, 2);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(path);
                var metadata = SpriteCatalogMetadata.CreateDefault(importer);
                metadata.SetIncluded(true);
                metadata.SetGroup(string.Empty);
                SpriteCatalogMetadata.Write(importer, metadata);

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Hero.png.*no group"));
                Invoke(typeof(SpriteSourceCollector), "ResolveMetadata", importer);
            }
            finally
            {
                File.WriteAllText(settingsFile, settings);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        // The window is closed, and the trace of the run stays open: nothing saves it.
        [Test]
        public void ProfilerTrace_OpenAtPlayModeExit_WindowClosed_IsFinished()
        {
            Assert.IsEmpty(Resources.FindObjectsOfTypeAll<ProfilerTraceWindow>(), "the test needs the window closed");

            var handlers = PlayModeHandlers();

            GameProfiler.Begin("EditorToolFailureTests");

            try
            {
                foreach (var handler in handlers.Where(item => item.Method.DeclaringType.FullName.StartsWith("Internal.ProfilerTrace", StringComparison.Ordinal)))
                    handler.DynamicInvoke(PlayModeStateChange.ExitingPlayMode);

                Assert.IsFalse(GameProfiler.IsRunning);
            }
            finally
            {
                GameProfiler.Finish();

                // The trace is a real file next to the project.
                var directory = Path.GetDirectoryName(ProfilerTraceStorage.LastPath);
                File.Delete(ProfilerTraceStorage.LastPath);

                if (Directory.GetFileSystemEntries(directory).Length == 0)
                    Directory.Delete(directory);
            }
        }

        // A mixed value of a [Flags] enum has no index.
        [Test]
        public void PrefabScreenshot_FlagsEnum_MixedValue_IsPrinted()
        {
            var asset = ScriptableObject.CreateInstance<FlagsHolder>();

            try
            {
                using var serialized = new SerializedObject(asset);
                var property = serialized.FindProperty(nameof(FlagsHolder.Value));
                Assert.AreEqual(-1, property.enumValueIndex, "not the case of the finding");

                Assert.AreEqual("3", Invoke(typeof(PrefabHierarchyDump), "GetPropertyValue", property));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void UserTab_GuestRecordIsNotJson_IsMalformed()
        {
            var read = typeof(UserTab).GetMethod("ReadGuestId", Flags);
            var environment = Enum.GetValues(read.GetParameters()[0].ParameterType).GetValue(0);
            var key = (string)Invoke(typeof(UserTab), "GuestKey", environment);
            var had = PlayerPrefs.HasKey(key);
            var old = PlayerPrefs.GetString(key, string.Empty);

            try
            {
                PlayerPrefs.SetString(key, "not a json");
                StringAssert.StartsWith("<malformed", (string)read.Invoke(null, new[] { environment }));
            }
            finally
            {
                if (had == true)
                    PlayerPrefs.SetString(key, old);
                else
                    PlayerPrefs.DeleteKey(key);
            }
        }

        // The event of EditorApplication lies in a tracker of its subscribers; only the handlers of the trace are called,
        // the others react to a play mode exit for real.
        private static Delegate[] PlayModeHandlers()
        {
            var tracker = typeof(EditorApplication).GetField("m_PlayModeStateChangedEvent", Flags).GetValue(null);
            var handlers = FindDelegates(tracker, 5).ToArray();
            Assert.IsNotEmpty(handlers, "the subscribers of the event are not found: Unity changed the tracker");
            return handlers;
        }

        private static IEnumerable<Delegate> FindDelegates(object value, int depth)
        {
            if (value is Action<PlayModeStateChange> action)
                return action.GetInvocationList();

            if (value == null || depth == 0 || value is string || value is Delegate || value.GetType().IsPrimitive)
                return Enumerable.Empty<Delegate>();

            if (value is IEnumerable items)
                return items.Cast<object>().SelectMany(item => FindDelegates(item, depth - 1));

            return value.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .SelectMany(field => FindDelegates(field.GetValue(value), depth - 1));
        }

        private static bool References(Type type, string method, MethodInfo target)
        {
            Assert.IsNotNull(target);

            var il = type.GetMethod(method, Flags).GetMethodBody().GetILAsByteArray();
            var token = BitConverter.GetBytes(target.MetadataToken);

            for (var i = 0; i <= il.Length - token.Length; i++)
            {
                if (il.Skip(i).Take(token.Length).SequenceEqual(token) == true)
                    return true;
            }

            return false;
        }

        private static object Invoke(Type type, string method, params object[] arguments)
        {
            return type.GetMethod(method, Flags).Invoke(null, arguments);
        }

        private class FlagsHolder : ScriptableObject
        {
            public MixedFlags Value = MixedFlags.A | MixedFlags.B;
        }

        [Flags]
        private enum MixedFlags
        {
            A = 1,
            B = 2,
            C = 4
        }
    }
}
