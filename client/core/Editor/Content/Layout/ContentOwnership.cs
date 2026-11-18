using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // Who holds what, the first part of ContentBundleLayout: the roots of each bundle (a root of two bundles is an error),
    // then every lazy target no bundle holds, given to the bundle that reaches it, with what it reaches in turn, until
    // nothing new comes. Walked in the order of the names of the bundles; a walk stops at an asset another bundle holds.
    // Every bundle of the project takes part, also one a build leaves out: the others hold the same as in a full build.
    public sealed class ContentOwnership
    {
        private readonly IReadOnlyList<ContentBundle> _bundles;
        private readonly ContentLazyReferenceScanner _lazy;
        private readonly ContentAssetGraph _graph;
        private readonly List<string> _errors;
        private readonly Dictionary<string, ContentAssetOwner> _owners = new();

        private ContentOwnership(IReadOnlyList<ContentBundle> bundles, ContentLazyReferenceScanner lazy, ContentAssetGraph graph, List<string> errors)
        {
            _bundles = bundles;
            _lazy = lazy;
            _graph = graph;
            _errors = errors;
        }

        // The errors of the roots go to errors.
        public static ContentOwnership Of(IReadOnlyList<ContentBundle> bundles, ContentLazyReferenceScanner lazy, ContentAssetGraph graph, List<string> errors)
        {
            var ownership = new ContentOwnership(bundles, lazy, graph, errors);
            ownership.ClaimRoots();
            ownership.ClaimLazyTargets();
            return ownership;
        }

        public bool TryGet(string path, out ContentAssetOwner owner)
        {
            return _owners.TryGetValue(path, out owner);
        }

        public bool Contains(string path)
        {
            return _owners.ContainsKey(path);
        }

        public ContentAssetOwner this[string path] => _owners[path];

        // The paths a bundle holds, in ordinal order.
        public IReadOnlyList<string> Own(ContentBundle bundle)
        {
            return _owners.Where(entry => entry.Value.Bundle == bundle).Select(entry => entry.Key).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        // The asset and everything it references directly, down to the assets another bundle holds; held is called with
        // each of those and its owner.
        public IEnumerable<string> Walk(ContentBundle bundle, string start, Action<string, ContentAssetOwner> held = null)
        {
            return _graph.Walk(start, path =>
            {
                if (_owners.TryGetValue(path, out var owner) == false || owner.Bundle == bundle)
                    return false;

                held?.Invoke(path, owner);
                return true;
            });
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

                    if (_owners.TryGetValue(path, out var owner))
                    {
                        _errors.Add($"{path} is a root of the content bundles {owner.Bundle.Name} and {bundle.Name}");
                        continue;
                    }

                    _owners.Add(path, new ContentAssetOwner(bundle, true, $"a root of {bundle.Name}"));
                }
            }
        }

        // The explicit assets of each bundle, walked in the order of the bundles: what they reach lazily and nobody holds
        // becomes theirs.
        private void ClaimLazyTargets()
        {
            var queue = new Queue<(ContentBundle, string)>(_owners.Select(entry => (entry.Value.Bundle, entry.Key)).OrderBy(entry => entry.Item1.Name, StringComparer.Ordinal));

            while (queue.Count > 0)
            {
                var (bundle, start) = queue.Dequeue();

                foreach (var path in Walk(bundle, start))
                {
                    foreach (var reference in _lazy.Of(path))
                    {
                        var target = ContentLazyTargets.PathOf(reference);

                        if (target == null || _owners.ContainsKey(target))
                            continue;

                        _owners.Add(target, new ContentAssetOwner(bundle, false, reference.Where));
                        queue.Enqueue((bundle, target));
                    }
                }
            }
        }
    }
}
