using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // Writes the shared content bundles of the game (IContentSharedBundles) from the layout of the others: every asset
    // the layout reports as needed directly by several bundles and held by none (ContentBundleLayout.Duplicates) becomes a
    // root of the first shared bundle that takes it, and the layout runs again with them until nothing is shared without a
    // holder. The shared bundles are written from nothing on every sync: a root nobody shares any more leaves, an empty
    // bundle is deleted; a sync with nothing new changes no file. Any other error of the layout fails the sync.
    public static class ContentSharedSync
    {
        private const string LogTag = "ContentSharedSync";
        private const int MaxPasses = 16;

        [MenuItem(FrameworkMenu.Codegen + "Sync Shared Content Bundles", priority = FrameworkMenu.TrailingPriority)]
        public static void Menu()
        {
            if (Run() == false)
                Debug.Log($"[{LogTag}] The project has no {nameof(IContentSharedBundles)}: nothing to sync");
        }

        // The shared bundles of the IContentSharedBundles of the project; false without one.
        public static bool Run()
        {
            var types = TypeCache.GetTypesDerivedFrom<IContentSharedBundles>()
                .Where(type => type.IsAbstract == false && type.ContainsGenericParameters == false)
                .ToArray();

            if (types.Length == 0)
                return false;

            if (types.Length > 1)
            {
                throw new InvalidOperationException(
                    $"[{LogTag}] {types.Length} classes implement {nameof(IContentSharedBundles)}, one expected: {string.Join(", ", types.Select(type => type.FullName))}");
            }

            var shared = (IContentSharedBundles)Activator.CreateInstance(types[0]);
            Sync(shared.Folder, shared.Bundles);
            return true;
        }

        // Against every other ContentBundle of the project; true when a file changed.
        public static bool Sync(string folder, IReadOnlyList<ContentSharedBundle> shared)
        {
            var names = new HashSet<string>(shared.Select(bundle => bundle.Name));
            var all = ContentBundleAssets.All();

            foreach (var stray in all.Where(bundle => names.Contains(bundle.Name) && AssetDatabase.GetAssetPath(bundle) != PathOf(folder, bundle.Name)))
                throw new InvalidOperationException($"[{LogTag}] {AssetDatabase.GetAssetPath(stray)}: a shared content bundle is written into {folder} only");

            return Sync(folder, shared, all.Where(bundle => names.Contains(bundle.Name) == false).ToArray());
        }

        // Against these bundles; true when a file changed.
        public static bool Sync(string folder, IReadOnlyList<ContentSharedBundle> shared, IReadOnlyList<ContentBundle> others)
        {
            Check(folder, shared);

            var roots = shared.ToDictionary(bundle => bundle.Name, _ => new SortedSet<string>(StringComparer.Ordinal));
            var lazy = new ContentLazyReferences();
            var graph = new ContentAssetGraph();

            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var transient = shared.Where(bundle => roots[bundle.Name].Count > 0).Select(bundle => Transient(bundle, roots[bundle.Name])).ToArray();
                ContentBundleLayout layout;

                try
                {
                    var bundles = others.Concat(transient).OrderBy(bundle => bundle.Name, StringComparer.Ordinal).ToArray();
                    layout = ContentBundleLayout.Inspect(bundles, Array.Empty<string>(), lazy, graph);
                }
                finally
                {
                    foreach (var bundle in transient)
                        Object.DestroyImmediate(bundle);
                }

                var rest = layout.Errors.Except(layout.Duplicates.Select(duplicate => duplicate.Error)).ToArray();

                if (rest.Length > 0)
                    throw new BuildFailedException($"[{LogTag}] The content bundles have errors no shared root fixes:\n{string.Join("\n", rest)}");

                if (layout.Duplicates.Count == 0)
                    return Write(folder, shared, roots);

                var added = 0;

                foreach (var duplicate in layout.Duplicates)
                {
                    var target = shared.FirstOrDefault(bundle => bundle.Takes(duplicate.Bundles)) ??
                                 throw new BuildFailedException($"[{LogTag}] No shared content bundle takes an asset of {string.Join(", ", duplicate.Bundles)}: {duplicate.Error}");

                    if (roots[target.Name].Add(duplicate.Path))
                        added++;
                }

                if (added == 0)
                    throw new BuildFailedException($"[{LogTag}] The shared roots do not settle:\n{string.Join("\n", layout.Errors)}");
            }

            throw new BuildFailedException($"[{LogTag}] The shared roots do not settle in {MaxPasses} passes");
        }

        private static void Check(string folder, IReadOnlyList<ContentSharedBundle> shared)
        {
            if (folder == null || folder.StartsWith("Assets/", StringComparison.Ordinal) == false || AssetDatabase.IsValidFolder(folder) == false)
                throw new InvalidOperationException($"[{LogTag}] The folder of the shared content bundles {folder} is no folder of the project (Assets/...)");

            var twice = shared.GroupBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);

            if (twice != null)
                throw new InvalidOperationException($"[{LogTag}] Two shared content bundles are named {twice.Key}");
        }

        // A shared bundle of this pass, in memory only: the layout reads its roots.
        private static ContentBundle Transient(ContentSharedBundle shared, IEnumerable<string> roots)
        {
            var bundle = ScriptableObject.CreateInstance<ContentBundle>();
            bundle.name = shared.Name;
            bundle.Init(roots.Select(AssetDatabase.LoadMainAssetAtPath).ToArray(), shared.IsPreloaded, shared.IsGzip);
            return bundle;
        }

        private static bool Write(string folder, IReadOnlyList<ContentSharedBundle> shared, IReadOnlyDictionary<string, SortedSet<string>> roots)
        {
            var changed = false;

            foreach (var definition in shared)
            {
                var path = PathOf(folder, definition.Name);
                var bundle = AssetDatabase.LoadAssetAtPath<ContentBundle>(path);
                var own = roots[definition.Name];

                if (own.Count == 0)
                {
                    if (bundle != null)
                    {
                        AssetDatabase.DeleteAsset(path);
                        Debug.Log($"[{LogTag}] {path}: nothing shared, deleted");
                        changed = true;
                    }

                    continue;
                }

                if (bundle == null)
                {
                    bundle = ScriptableObject.CreateInstance<ContentBundle>();
                    AssetDatabase.CreateAsset(bundle, path);
                }

                var objects = own.Select(AssetDatabase.LoadMainAssetAtPath).ToArray();

                if (bundle.Roots.SequenceEqual(objects) && bundle.IsPreloaded == definition.IsPreloaded && bundle.IsGzip == definition.IsGzip)
                    continue;

                bundle.Init(objects, definition.IsPreloaded, definition.IsGzip);
                EditorUtility.SetDirty(bundle);
                AssetDatabase.SaveAssetIfDirty(bundle);
                Debug.Log($"[{LogTag}] {path}: {own.Count} root(s)");
                changed = true;
            }

            return changed;
        }

        private static string PathOf(string folder, string name)
        {
            return $"{folder}/{name}.asset";
        }
    }
}
