using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Internal
{
    // Full hierarchy mirror: the generator walks the object top to bottom, including the contents of
    // nested prefabs. There are only two boundaries - an object with its own bindings (its own class is
    // responsible for its insides, and a reference to it resets the serialization depth, of which Unity
    // has only seven levels) and HierarchyBindingsIgnoreChildren, which cuts off unneeded branches.
    //
    // On a prefab variant the walk is also filtered by the layer (HierarchyBindingsLayer): foreign
    // components are skipped, an inherited object without own content drops out entirely, and one with
    // added content inside stays as a pass-through node - without a reference to the object itself, only
    // with its own components and children.
    internal static class HierarchyBindingsScanner
    {
        // Unity stops serializing nested non-Object types deeper than seven levels, so
        // going further down the tree is pointless: the fields simply will not be saved.
        public const int MaxNestingDepth = 7;

        // Minimum group size. A pair of identical siblings is most often meaningfully named
        // separately (Left/Right), while a row of three or more is already a list.
        public const int MinGroupSize = 3;

        // The registration flags decide which methods the root class gets (Create, Register), so they are part of
        // the names the root members must not take.
        public static HierarchyBindingsNode Scan(
            GameObject root,
            string rootTypeName,
            HierarchyBindingsLayer layer,
            List<string> errors,
            bool isSceneService,
            bool isEntityComponent)
        {
            if (root == null)
            {
                errors.Add("Object is null.");
                return null;
            }

            var members = new IdentifierScope(rootTypeName, RootReservedMembers(isSceneService, isEntityComponent));
            var node = ScanNode(root, rootTypeName, root.name, 0, layer, members, errors);
            return errors.Count == 0 ? node : null;
        }

        // The root class derives from ObjectBindings: its visible members would be hidden (CS0108), and its private
        // serialized fields may not be serialized twice by a derived class.
        private static IEnumerable<string> RootReservedMembers(bool isSceneService, bool isEntityComponent)
        {
            var reserved = new List<string>(CodeIdentifiers.InheritedMembers(typeof(ObjectBindings)));

            foreach (var field in typeof(ObjectBindings).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                reserved.Add(field.Name);

            if (isSceneService)
                reserved.Add(HierarchyBindingsCodeGenerator.SceneServiceMethod);

            if (isEntityComponent)
                reserved.Add(HierarchyBindingsCodeGenerator.EntityComponentMethod);

            return reserved;
        }

        // A nested class is a plain [Serializable] class: only the members of object are its own.
        private static IEnumerable<string> NestedReservedMembers()
        {
            return CodeIdentifiers.InheritedMembers(typeof(object));
        }

        private static HierarchyBindingsNode ScanNode(
            GameObject target,
            string typeName,
            string hierarchyPath,
            int depth,
            HierarchyBindingsLayer layer,
            IdentifierScope used,
            List<string> errors)
        {
            var ignoreChildren = target.GetComponent<HierarchyBindingsIgnoreChildren>() != null;
            var node = new HierarchyBindingsNode(target, typeName, hierarchyPath, used, ignoreChildren);

            // A reference to an inherited object belongs to the layer of the base. The root is an exception: it
            // carries the bindings themselves and always has the property.
            if (depth == 0 || layer.IsOwn(target))
                HierarchyBindingsComponentFields.CollectGameObject(node, used);

            HierarchyBindingsComponentFields.CollectComponents(node, used, depth, layer, errors);

            if (node.IgnoreChildren == false)
                CollectChildren(node, used, depth, layer, errors);

            return node;
        }

        private static void CollectChildren(
            HierarchyBindingsNode node,
            IdentifierScope used,
            int depth,
            HierarchyBindingsLayer layer,
            List<string> errors)
        {
            var scans = ScanChildren(node, depth, layer, errors);
            var siblingNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scan in scans)
                siblingNames.Add(HierarchyBindingsNaming.ToIdentifier(scan.Child.name));

            for (var index = 0; index < scans.Count;)
            {
                var length = HierarchyBindingsChildMembers.GroupLength(scans, index);

                if (length >= MinGroupSize &&
                    HierarchyBindingsChildMembers.TryAddGroup(node, scans, index, length, used, siblingNames, errors))
                {
                    index += length;
                    continue;
                }

                HierarchyBindingsChildMembers.AddSingle(node, scans[index], used, siblingNames, errors);
                index++;
            }
        }

        // Names at the parent level are assigned only after it is known what collapses into what:
        // otherwise taken names would go to siblings that will no longer be in the class.
        private static List<HierarchyBindingsChildScan> ScanChildren(
            HierarchyBindingsNode node,
            int depth,
            HierarchyBindingsLayer layer,
            List<string> errors)
        {
            var transform = node.Target.transform;
            var scans = new List<HierarchyBindingsChildScan>(transform.childCount);

            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index).gameObject;
                var childPath = node.HierarchyPath + "/" + child.name;
                var scan = new HierarchyBindingsChildScan(child, childPath, HierarchyBindingsNaming.StripIndexSuffix(child.name));

                var boundaries = HierarchyBindingsComponentFields.FindBindings(child, childPath, layer, errors);

                if (boundaries.Count > 0)
                {
                    scan.SetBoundaries(boundaries, HierarchyBindingsSignature.ComputeBoundarySignature(boundaries));
                    scans.Add(scan);
                    continue;
                }

                // An inherited branch where the variant added nothing is not in its layer - neither the depth
                // nor the contents concern it.
                if (layer.IsVariant && ContainsOwnContent(child, layer) == false)
                    continue;

                if (depth + 1 > MaxNestingDepth)
                {
                    errors.Add(
                            $"'{childPath}' is deeper than {MaxNestingDepth} levels. Unity stops serializing there — " +
                            "give an intermediate object its own bindings to break the chain."
                        );
                    continue;
                }

                if (layer.IsOwn(child) == false &&
                    layer.TryValidatePassThrough(child, childPath, errors) == false)
                    continue;

                var childNode = ScanNode(child, null, childPath, depth + 1, layer,
                    new IdentifierScope(null, NestedReservedMembers()), errors);
                scan.SetNode(childNode, HierarchyBindingsSignature.ComputeSignature(childNode));
                scans.Add(scan);
            }

            return scans;
        }

        // Whether the branch has anything of the variant layer: an own object or an own component.
        private static bool ContainsOwnContent(GameObject target, HierarchyBindingsLayer layer)
        {
            if (layer.IsOwn(target))
                return true;

            foreach (var component in target.GetComponents<Component>())
            {
                // A missing script is not decided here: the walk of the object itself reports it loudly.
                if (component == null)
                    return true;

                if (layer.IsOwn(component))
                    return true;
            }

            // The walk does not mirror the branch under the marker anyway, what was added there does not count.
            if (target.GetComponent<HierarchyBindingsIgnoreChildren>() != null)
                return false;

            foreach (Transform child in target.transform)
            {
                if (ContainsOwnContent(child.gameObject, layer))
                    return true;
            }

            return false;
        }

        public static string ToCodeTypeName(Type type)
        {
            var fullName = type.FullName ?? type.Name;
            return "global::" + fullName.Replace('+', '.');
        }
    }
}
