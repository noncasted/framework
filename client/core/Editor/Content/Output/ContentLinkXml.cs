using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // The types the objects of the bundles have (their assets, what the engine copies in, the components of their prefabs
    // and scenes), as a link.xml for the linker of the player build (ContentLinkerProcessor): a type only a bundle uses (a
    // collider of a level, a component of a prefab) is no longer stripped, and the engine keeps its native class. The type
    // only (preserve="nothing"): its members are the ones the linker keeps for any type it keeps (the serialized fields and
    // the messages of a MonoBehaviour); "all" kept every method of every type, +115 KB of wasm and +144 KB of .data on the
    // game of the jam.
    public static class ContentLinkXml
    {
        public const string Output = "Library/ContentBundles.link.xml";

        public static string Write(IReadOnlyList<ContentBuiltBundle> bundles, string path = Output)
        {
            var types = new HashSet<Type>();

            foreach (var bundle in bundles)
            {
                foreach (var asset in bundle.Plan.Assets.Concat(bundle.Plan.Implicit.Keys))
                    AddAsset(asset, types);

                foreach (var scene in bundle.Plan.Scenes)
                    AddScene(scene, types);
            }

            var assemblies = types
                .Where(type => type.Assembly.IsDynamic == false)
                .GroupBy(type => type.Assembly.GetName().Name)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"  <assembly fullname=\"{SecurityElement.Escape(group.Key)}\">\n" +
                                 string.Concat(group.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal)
                                     .Select(name => $"    <type fullname=\"{SecurityElement.Escape(name)}\" preserve=\"nothing\" />\n")) +
                                 "  </assembly>\n");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? throw new IOException(path));
            File.WriteAllText(path, $"<linker>\n{string.Concat(assemblies)}</linker>\n");
            return Path.GetFullPath(path);
        }

        private static void AddAsset(string path, HashSet<Type> types)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                Add(asset, types);
        }

        // A preview scene: the scenes open in the editor stay as they are.
        private static void AddScene(string path, HashSet<Type> types)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                        Add(component, types);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Add(Object asset, HashSet<Type> types)
        {
            // A missing script, or an object of the editor only (a script asset, an importer of a sub-asset).
            if (asset == null || asset is MonoScript || asset.GetType().Assembly.GetName().Name.StartsWith("UnityEditor", StringComparison.Ordinal))
                return;

            types.Add(asset.GetType());
        }
    }
}
