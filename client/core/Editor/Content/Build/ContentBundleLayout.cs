using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    // Lays out the bundles of the project before their build and checks that every dependency has one owner:
    // 1. the roots of each ContentBundle are its own (a root of two bundles is an error);
    // 2. a lazy target (ContentReference, ContentScene) no bundle holds goes into the bundle that reaches it, with what it
    //    reaches in turn, until nothing new comes; a walk stops at an asset another bundle holds (a reference across bundles,
    //    which the engine resolves when that bundle is loaded: the catalog lists it as a dependency);
    // 3. the checks, all errors at once: an empty or broken lazy reference; a lazy target no root holds that two bundles
    //    reach; an asset two bundles reach directly and none holds (the engine would copy it into both); a lazy target the
    //    loader cannot find in its bundle (a child of a prefab).
    // An asset the assets and the scenes of one bundle both need goes to its assets (the engine keeps the two apart).
    public sealed class ContentBundleLayout
    {
        private readonly IReadOnlyList<ContentBundle> _bundles;
        private readonly ContentLazyReferences _lazy = new();
        private readonly ContentAssetGraph _graph = new();
        private readonly Dictionary<(string, long), string> _objectReferences = new();
        private readonly Dictionary<string, Claim> _claims = new();
        private readonly List<string> _errors = new();

        private ContentBundleLayout(IReadOnlyList<ContentBundle> bundles)
        {
            _bundles = bundles;
        }

        public IReadOnlyList<ContentBundlePlan> Plans { get; private set; }

        // Throws BuildFailedException with every error.
        public static ContentBundleLayout Lay(IReadOnlyList<ContentBundle> bundles)
        {
            if (bundles.Count == 0)
                throw new BuildFailedException("[ContentBuild] The project has no ContentBundle asset: a player build needs at least the main bundle");

            ContentBundleAssets.Check(bundles);

            var layout = new ContentBundleLayout(bundles);
            layout.ClaimRoots();
            layout.ClaimLazyTargets();
            layout.Plans = layout.Check();

            if (layout._errors.Count > 0)
            {
                throw new BuildFailedException(
                    $"[ContentBuild] The content bundles have {layout._errors.Count} errors:\n{string.Join("\n", layout._errors.Distinct())}");
            }

            return layout;
        }

        private void ClaimRoots()
        {
            foreach (var bundle in _bundles)
            {
                if (bundle.Roots.Count == 0)
                    _errors.Add($"{AssetDatabase.GetAssetPath(bundle)}: the content bundle {bundle.Name} has no roots");

                foreach (var root in bundle.Roots)
                {
                    var path = root == null ? null : AssetDatabase.GetAssetPath(root);

                    if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path) || ContentAssetGraph.IsIgnored(path) || AssetDatabase.IsMainAsset(root) == false)
                    {
                        _errors.Add($"{AssetDatabase.GetAssetPath(bundle)}: the root {(root == null ? "(empty)" : path)} of {bundle.Name} is no asset or scene of the project");
                        continue;
                    }

                    if (_claims.TryGetValue(path, out var claim))
                    {
                        _errors.Add($"{path} is a root of the content bundles {claim.Bundle.Name} and {bundle.Name}");
                        continue;
                    }

                    _claims.Add(path, new Claim(bundle, true, $"a root of {bundle.Name}"));
                }
            }
        }

        // The explicit assets of each bundle, walked in the order of the bundles: what they reach lazily and nobody holds
        // becomes theirs.
        private void ClaimLazyTargets()
        {
            var queue = new Queue<(ContentBundle, string)>(_claims.Select(entry => (entry.Value.Bundle, entry.Key)).OrderBy(entry => entry.Item1.Name, StringComparer.Ordinal));

            while (queue.Count > 0)
            {
                var (bundle, start) = queue.Dequeue();

                foreach (var path in Walk(bundle, start))
                {
                    foreach (var reference in _lazy.Of(path))
                    {
                        var target = ContentLazyTargets.PathOf(reference);

                        if (target == null || _claims.ContainsKey(target))
                            continue;

                        _claims.Add(target, new Claim(bundle, false, reference.Where));
                        queue.Enqueue((bundle, target));
                    }
                }
            }
        }

        private IReadOnlyList<ContentBundlePlan> Check()
        {
            var plans = _bundles.Select(bundle => new ContentBundlePlan(bundle)).ToArray();
            var implicitOwners = new Dictionary<string, List<(ContentBundle, string)>>();
            var lazyReach = new Dictionary<string, List<(ContentBundle, string)>>();

            foreach (var plan in plans)
            {
                var own = _claims.Where(entry => entry.Value.Bundle == plan.Bundle).Select(entry => entry.Key).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                plan.Scenes.AddRange(own.Where(ContentAssetGraph.IsScene));
                plan.Assets.AddRange(own.Where(path => ContentAssetGraph.IsScene(path) == false));
                plan.Roots.AddRange(plan.Bundle.Roots.Where(root => root != null && ContentAssetGraph.IsScene(AssetDatabase.GetAssetPath(root)) == false)
                    .Select(root => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root))));

                var fromAssets = Reach(plan.Bundle, plan.Assets, lazyReach);
                var fromScenes = Reach(plan.Bundle, plan.Scenes, lazyReach);

                // Needed by both parts: into the assets, so the bundle of the scenes references it.
                foreach (var shared in fromAssets.Keys.Intersect(fromScenes.Keys).OrderBy(path => path, StringComparer.Ordinal).ToArray())
                {
                    plan.Assets.Add(shared);
                    fromAssets.Remove(shared);
                    fromScenes.Remove(shared);
                }

                foreach (var entry in fromAssets.Concat(fromScenes))
                {
                    plan.Implicit[entry.Key] = entry.Value;

                    if (implicitOwners.TryGetValue(entry.Key, out var owners) == false)
                        implicitOwners.Add(entry.Key, owners = new List<(ContentBundle, string)>());

                    owners.Add((plan.Bundle, entry.Value));
                }
            }

            foreach (var entry in implicitOwners.Where(entry => entry.Value.Count > 1).OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                _errors.Add($"{entry.Key} is needed by the content bundles {string.Join(" and ", entry.Value.Select(owner => $"{owner.Item1.Name} (through {owner.Item2})"))}: " +
                            "put it into one of them as a root, or into a shared ContentBundle, or the engine copies it into each");
            }

            foreach (var entry in lazyReach.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var claim = _claims[entry.Key];
                var others = entry.Value.Where(reach => reach.Item1 != claim.Bundle).ToArray();

                if (claim.IsRoot || others.Length == 0)
                    continue;

                _errors.Add($"{entry.Key} is reached lazily by the content bundles {claim.Bundle.Name} ({claim.Where}) and " +
                            $"{string.Join(", ", others.Select(reach => $"{reach.Item1.Name} ({reach.Item2})"))}: make it a root of the bundle that should hold it");
            }

            foreach (var plan in plans)
                AddEntries(plan);

            return plans;
        }

        // What the explicit assets of a part of a bundle reach directly and no bundle holds, with the asset that reaches it;
        // the lazy targets they hold go to lazyReach (the target and the bundles that reach it).
        private Dictionary<string, string> Reach(ContentBundle bundle, IReadOnlyList<string> starts, Dictionary<string, List<(ContentBundle, string)>> lazyReach)
        {
            var reached = new Dictionary<string, string>();

            foreach (var start in starts)
            {
                foreach (var path in Walk(bundle, start))
                {
                    if (_claims.ContainsKey(path) == false && reached.ContainsKey(path) == false)
                        reached.Add(path, start);

                    foreach (var reference in _lazy.Of(path))
                    {
                        var target = Checked(reference);

                        if (target == null)
                            continue;

                        if (reference.IsScene == false)
                            _objectReferences.TryAdd((reference.Guid, reference.LocalId), reference.Where);

                        if (lazyReach.TryGetValue(target, out var reach) == false)
                            lazyReach.Add(target, reach = new List<(ContentBundle, string)>());

                        if (reach.Any(entry => entry.Item1 == bundle) == false)
                            reach.Add((bundle, reference.Where));
                    }
                }
            }

            return reached;
        }

        // The lazy targets a bundle owns, and its scenes, for the catalog: every target any bundle references.
        private void AddEntries(ContentBundlePlan plan)
        {
            foreach (var scene in plan.Scenes)
                plan.SceneEntries.Add(new ContentCatalogScene(AssetDatabase.AssetPathToGUID(scene), plan.Name, scene));

            foreach (var entry in _objectReferences.OrderBy(entry => entry.Key))
            {
                var (guid, localId) = entry.Key;
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (_claims.TryGetValue(path, out var claim) == false || claim.Bundle != plan.Bundle)
                    continue;

                var subName = ContentLazyTargets.SubName(ContentEditorAssets.Find(guid, localId), path, out var error);

                if (subName == null)
                    _errors.Add($"{entry.Value}: {error}");
                else
                    plan.Objects.Add(new ContentCatalogObject(guid, localId, plan.Name, subName));
            }
        }

        // The path of the target of a lazy reference, null (with an error) for an empty or broken one.
        private string Checked(ContentLazyReference reference)
        {
            if (reference.IsEmpty)
            {
                _errors.Add($"{reference.Where}: empty");
                return null;
            }

            var path = ContentLazyTargets.PathOf(reference);

            if (path == null)
                _errors.Add($"{reference.Where}: {reference.Guid}:{reference.LocalId} is no {(reference.IsScene ? "scene" : "object")} of the project (assign it in the inspector)");

            return path;
        }

        // The asset and everything it references directly, down to the assets another bundle holds.
        private IEnumerable<string> Walk(ContentBundle bundle, string start)
        {
            return _graph.Walk(start, path => _claims.TryGetValue(path, out var claim) && claim.Bundle != bundle);
        }

        private readonly struct Claim
        {
            public Claim(ContentBundle bundle, bool isRoot, string where)
            {
                Bundle = bundle;
                IsRoot = isRoot;
                Where = where;
            }

            public ContentBundle Bundle { get; }
            public bool IsRoot { get; }
            public string Where { get; }
        }
    }
}
