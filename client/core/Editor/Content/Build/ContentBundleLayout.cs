using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    // Lays out the bundles of the project before their build and checks that every dependency has one owner:
    // 1. who holds what (ContentBundleClaims): the roots of each bundle, then the lazy targets (ContentReference,
    //    ContentScene) no bundle holds, given to the bundle that reaches them; a walk stops at an asset another bundle holds
    //    (a reference across bundles, which the engine resolves when that bundle is loaded: the catalog lists it as a dependency);
    // 2. the checks, all errors at once: an empty or broken lazy reference; a lazy target no root holds that two bundles
    //    reach; an asset two bundles reach directly and none holds (the engine would copy it into both: also as data,
    //    Duplicates); a lazy target the loader cannot find in its bundle (a child of a prefab); a reference of a built bundle
    //    to what a bundle left out of this build holds.
    // A bundle left out of the build (ContentBuildContext.Exclude) still holds what it holds, so the others hold the same as
    // in a full build; it gets no plan and nothing of it is checked. An asset the assets and the scenes of one bundle both
    // need goes to its assets (the engine keeps the two apart); another bundle that needs it too still makes it a duplicate.
    public sealed class ContentBundleLayout
    {
        private readonly IReadOnlyList<ContentBundle> _bundles;
        private readonly HashSet<ContentBundle> _excluded;
        private readonly ContentLazyReferences _lazy;
        private readonly ContentAssetGraph _graph;
        private readonly Dictionary<(string, long), string> _objectReferences = new();
        private readonly List<string> _errors = new();
        private readonly List<ContentDuplicate> _duplicates = new();
        private ContentBundleClaims _claims;

        private ContentBundleLayout(IReadOnlyList<ContentBundle> bundles, HashSet<ContentBundle> excluded, ContentLazyReferences lazy, ContentAssetGraph graph)
        {
            _bundles = bundles;
            _excluded = excluded;
            _lazy = lazy;
            _graph = graph;
        }

        // The bundles of the build, without the excluded ones.
        public IReadOnlyList<ContentBundlePlan> Plans { get; private set; }

        // Every error, the duplicates included, one a line.
        public IReadOnlyList<string> Errors => _errors.Distinct().ToArray();

        public IReadOnlyList<ContentDuplicate> Duplicates => _duplicates;

        // Throws BuildFailedException with every error.
        public static ContentBundleLayout Lay(IReadOnlyList<ContentBundle> bundles)
        {
            return Lay(bundles, Array.Empty<string>());
        }

        // Without the bundles of these names; throws BuildFailedException with every error.
        public static ContentBundleLayout Lay(IReadOnlyList<ContentBundle> bundles, IReadOnlyCollection<string> excluded)
        {
            var layout = Inspect(bundles, excluded);
            var errors = layout.Errors;

            if (errors.Count > 0)
                throw new BuildFailedException($"[ContentBuild] The content bundles have {errors.Count} errors:\n{string.Join("\n", errors)}");

            return layout;
        }

        // The layout with its errors as they are, for a tool that fixes them; throws only for a set of bundles it cannot lay out.
        public static ContentBundleLayout Inspect(IReadOnlyList<ContentBundle> bundles, IReadOnlyCollection<string> excluded)
        {
            return Inspect(bundles, excluded, new ContentLazyReferences(), new ContentAssetGraph());
        }

        // With the references read by a layout before (the assets did not change since): a tool that lays out several times.
        public static ContentBundleLayout Inspect(IReadOnlyList<ContentBundle> bundles, IReadOnlyCollection<string> excluded, ContentLazyReferences lazy,
            ContentAssetGraph graph)
        {
            if (bundles.Count == 0)
                throw new BuildFailedException("[ContentBuild] The project has no ContentBundle asset: a player build needs at least the main bundle");

            ContentBundleAssets.Check(bundles);

            var unknown = excluded.Where(name => bundles.Any(bundle => bundle.Name == name) == false).ToArray();

            if (unknown.Length > 0)
            {
                throw new BuildFailedException($"[ContentBuild] No content bundle {string.Join(", ", unknown)} to leave out of the build: " +
                                               $"the project has {string.Join(", ", bundles.Select(bundle => bundle.Name))}");
            }

            if (bundles.All(bundle => excluded.Contains(bundle.Name)))
                throw new BuildFailedException("[ContentBuild] The build leaves out every content bundle: keep at least the main one");

            var layout = new ContentBundleLayout(bundles, new HashSet<ContentBundle>(bundles.Where(bundle => excluded.Contains(bundle.Name))), lazy, graph);
            layout._claims = ContentBundleClaims.Of(bundles, layout._lazy, layout._graph, layout._errors);
            layout.Plans = layout.Check();
            return layout;
        }

        private IReadOnlyList<ContentBundlePlan> Check()
        {
            var plans = _bundles.Where(bundle => _excluded.Contains(bundle) == false).Select(bundle => new ContentBundlePlan(bundle)).ToArray();
            var implicitOwners = new Dictionary<string, List<(ContentBundle, string)>>();
            var lazyReach = new Dictionary<string, List<(ContentBundle, string)>>();

            foreach (var plan in plans)
            {
                var own = _claims.Own(plan.Bundle);
                plan.Scenes.AddRange(own.Where(ContentAssetGraph.IsScene));
                plan.Assets.AddRange(own.Where(path => ContentAssetGraph.IsScene(path) == false));
                plan.Roots.AddRange(plan.Bundle.Roots.Where(root => root != null && ContentAssetGraph.IsScene(AssetDatabase.GetAssetPath(root)) == false)
                    .Select(root => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root))));

                var fromAssets = Reach(plan.Bundle, plan.Assets, lazyReach);
                var fromScenes = Reach(plan.Bundle, plan.Scenes, lazyReach);

                // Needed by both parts: into the assets, so the bundle of the scenes references it. Still needed by this bundle
                // without a holder: another bundle that needs it too makes it a duplicate.
                foreach (var shared in fromAssets.Keys.Intersect(fromScenes.Keys).OrderBy(path => path, StringComparer.Ordinal).ToArray())
                {
                    plan.Assets.Add(shared);
                    AddOwner(implicitOwners, shared, plan.Bundle, fromAssets[shared]);
                    fromAssets.Remove(shared);
                    fromScenes.Remove(shared);
                }

                foreach (var entry in fromAssets.Concat(fromScenes))
                {
                    plan.Implicit[entry.Key] = entry.Value;
                    AddOwner(implicitOwners, entry.Key, plan.Bundle, entry.Value);
                }
            }

            foreach (var entry in implicitOwners.Where(entry => entry.Value.Count > 1).OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var error = $"{entry.Key} is needed by the content bundles {string.Join(" and ", entry.Value.Select(owner => $"{owner.Item1.Name} (through {owner.Item2})"))}: " +
                            "put it into one of them as a root, or into a shared ContentBundle, or the engine copies it into each";
                _errors.Add(error);
                _duplicates.Add(new ContentDuplicate(entry.Key, entry.Value.Select(owner => owner.Item1.Name).ToArray(), error));
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

        private static void AddOwner(Dictionary<string, List<(ContentBundle, string)>> owners, string path, ContentBundle bundle, string through)
        {
            if (owners.TryGetValue(path, out var list) == false)
                owners.Add(path, list = new List<(ContentBundle, string)>());

            list.Add((bundle, through));
        }

        // What the explicit assets of a part of a bundle reach directly and no bundle holds, with the asset that reaches it;
        // the lazy targets they hold go to lazyReach (the target and the bundles that reach it).
        private Dictionary<string, string> Reach(ContentBundle bundle, IReadOnlyList<string> starts, Dictionary<string, List<(ContentBundle, string)>> lazyReach)
        {
            var reached = new Dictionary<string, string>();

            foreach (var start in starts)
            {
                foreach (var path in _claims.Walk(bundle, start, (held, claim) => CheckBuilt(claim, $"{start} of {bundle.Name} needs {held}")))
                {
                    if (_claims.Contains(path) == false && reached.ContainsKey(path) == false)
                        reached.Add(path, start);

                    foreach (var reference in _lazy.Of(path))
                    {
                        var target = Checked(reference);

                        if (target == null || CheckBuilt(_claims[target], $"{reference.Where} references {target}") == false)
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

        // False (with an error) for what a bundle left out of this build holds: a built bundle cannot depend on it.
        private bool CheckBuilt(ContentBundleClaim claim, string what)
        {
            if (_excluded.Contains(claim.Bundle) == false)
                return true;

            _errors.Add($"{what}, held by the content bundle {claim.Bundle.Name}, which this build leaves out (ContentBuildContext.Exclude): " +
                        $"keep {claim.Bundle.Name} in this build or drop the reference");
            return false;
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

                if (_claims.TryGet(path, out var claim) == false || claim.Bundle != plan.Bundle)
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
    }
}
