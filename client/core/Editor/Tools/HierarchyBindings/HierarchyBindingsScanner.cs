using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Scripting.LifecycleManagement;
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
    [NoAutoStaticsCleanup]
    internal static class HierarchyBindingsScanner
    {
        // Unity stops serializing nested non-Object types deeper than seven levels, so
        // going further down the tree is pointless: the fields simply will not be saved.
        public const int MaxNestingDepth = 7;

        // Minimum group size. A pair of identical siblings is most often meaningfully named
        // separately (Left/Right), while a row of three or more is already a list.
        public const int MinGroupSize = 3;

        private static readonly HashSet<Type> SkippedComponents = new()
        {
            typeof(CanvasRenderer),
            typeof(HierarchyBindingsIgnoreChildren)
        };

        public static HierarchyBindingsNode Scan(
            GameObject root,
            string rootTypeName,
            HierarchyBindingsLayer layer,
            List<string> errors)
        {
            if (root == null)
            {
                errors.Add("Object is null.");
                return null;
            }

            var node = ScanNode(root, rootTypeName, root.name, 0, layer, errors);
            return errors.Count == 0 ? node : null;
        }

        private static HierarchyBindingsNode ScanNode(
            GameObject target,
            string typeName,
            string hierarchyPath,
            int depth,
            HierarchyBindingsLayer layer,
            List<string> errors)
        {
            var node = new HierarchyBindingsNode
            {
                Target = target,
                TypeName = typeName,
                HierarchyPath = hierarchyPath
            };

            node.IgnoreChildren = target.GetComponent<HierarchyBindingsIgnoreChildren>() != null;

            var used = new HashSet<string>(StringComparer.Ordinal);

            // A reference to an inherited object belongs to the layer of the base. The root is an exception: it
            // carries the bindings themselves and always has the property.
            if (depth == 0 || layer.IsOwn(target))
                CollectGameObject(node, used);

            CollectComponents(node, used, depth, layer, errors);

            if (node.IgnoreChildren == false)
                CollectChildren(node, used, depth, layer, errors);

            return node;
        }

        // Everyone needs the object itself: nested classes have no other path to it, and for the root
        // the property just duplicates MonoBehaviour.gameObject for uniform access.
        private static void CollectGameObject(HierarchyBindingsNode node, HashSet<string> used)
        {
            var propertyName = HierarchyBindingsNaming.MakeUnique("GameObject", used);

            node.Fields.Add(new HierarchyBindingsField
            {
                Target = node.Target,
                PropertyName = propertyName,
                FieldName = HierarchyBindingsNaming.ToFieldName(propertyName),
                CodeTypeName = "global::UnityEngine.GameObject"
            });
        }

        private static void CollectComponents(
            HierarchyBindingsNode node,
            HashSet<string> used,
            int depth,
            HierarchyBindingsLayer layer,
            List<string> errors)
        {
            foreach (var component in node.Target.GetComponents<Component>())
            {
                if (component == null)
                {
                    errors.Add($"'{node.HierarchyPath}' has a missing script. Fix the object before generating.");
                    continue;
                }

                var type = component.GetType();

                if (SkippedComponents.Contains(type))
                    continue;

                // Components created at edit time and never saved (TextMeshPro adds a HideAndDontSave MeshFilter
                // in Awake). Whether they exist depends on how the object was loaded: a prefab asset from
                // LoadAssetAtPath has none, LoadPrefabContents runs Awake and has them. A reference to one
                // serializes as null anyway.
                if ((component.hideFlags & HideFlags.DontSaveInEditor) != 0)
                    continue;

                // A non-public type (internal components of a package) cannot be declared as a field of the
                // generated class - CS0122.
                if (type.IsVisible == false)
                    continue;

                // Scripts without an asmdef live in Assembly-CSharp, which the asmdef assembly of the generated
                // class cannot see - CS0400.
                if (IsPredefinedAssembly(type))
                    continue;

                // The bindings owner is the generated component itself (or the user class
                // that inherits it). A self reference is not needed in the mirror.
                if (depth == 0 && component is IObjectBindings)
                    continue;

                if (layer.IsOwn(component) == false)
                    continue;

                var field = MakeField(component, type.Name, used, errors, node.HierarchyPath);

                if (field != null)
                    node.Fields.Add(field);
            }
        }

        private static void CollectChildren(
            HierarchyBindingsNode node,
            HashSet<string> used,
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
                var length = GroupLength(scans, index);

                if (length >= MinGroupSize && TryAddGroup(node, scans, index, length, used, siblingNames, errors))
                {
                    index += length;
                    continue;
                }

                AddSingle(node, scans[index], used, siblingNames, errors);
                index++;
            }
        }

        // Names at the parent level are assigned only after it is known what collapses into what:
        // otherwise taken names would go to siblings that will no longer be in the class.
        private static List<ChildScan> ScanChildren(
            HierarchyBindingsNode node,
            int depth,
            HierarchyBindingsLayer layer,
            List<string> errors)
        {
            var transform = node.Target.transform;
            var scans = new List<ChildScan>(transform.childCount);

            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index).gameObject;
                var childPath = node.HierarchyPath + "/" + child.name;
                var scan = new ChildScan
                {
                    Child = child,
                    ChildPath = childPath,
                    BaseName = HierarchyBindingsNaming.StripIndexSuffix(child.name)
                };

                var boundaries = FindBindings(child, childPath, layer, errors);

                if (boundaries.Count > 0)
                {
                    scan.Boundaries = boundaries;
                    scan.Signature = ComputeBoundarySignature(boundaries);
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

                scan.Node = ScanNode(child, null, childPath, depth + 1, layer, errors);
                scan.Signature = ComputeSignature(scan.Node);
                scans.Add(scan);
            }

            return scans;
        }

        // Only consecutive siblings with a common name and matching contents are collapsed: the array
        // must read the same way as the row in the hierarchy, and the class is shared by all.
        private static int GroupLength(List<ChildScan> scans, int start)
        {
            var first = scans[start];

            if (string.IsNullOrEmpty(HierarchyBindingsNaming.ToIdentifier(first.BaseName)))
                return 1;

            var length = 1;

            while (start + length < scans.Count)
            {
                var next = scans[start + length];

                if (string.Equals(next.BaseName, first.BaseName, StringComparison.Ordinal) == false)
                    break;

                if (string.Equals(next.Signature, first.Signature, StringComparison.Ordinal) == false)
                    break;

                length++;
            }

            return length;
        }

        private static bool TryAddGroup(
            HierarchyBindingsNode node,
            List<ChildScan> scans,
            int start,
            int length,
            HashSet<string> used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            var first = scans[start];
            var baseName = HierarchyBindingsNaming.ToIdentifier(first.BaseName);

            if (Validate(baseName, first.BaseName, first.ChildPath, errors) == false)
                return false;

            var comment = $"{first.ChildPath} .. {scans[start + length - 1].Child.name} ({length} items)";

            if (first.Boundaries != null)
            {
                AddBoundaryGroup(node, scans, start, length, baseName, comment, used, siblingNames, errors);
                return true;
            }

            var propertyName = HierarchyBindingsNaming.MakeUnique(
                HierarchyBindingsNaming.ToPlural(baseName), used);
            var fieldName = HierarchyBindingsNaming.ToFieldName(propertyName);

            var template = first.Node;
            template.TypeName = HierarchyBindingsNaming.MakeUnique(baseName + "Bindings", used);
            template.PropertyName = propertyName;
            template.FieldName = fieldName;

            for (var index = start; index < start + length; index++)
                template.Elements.Add(scans[index].Node);

            node.Children.Add(template);
            return true;
        }

        private static void AddSingle(
            HierarchyBindingsNode node,
            ChildScan scan,
            HashSet<string> used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            if (scan.Boundaries != null)
            {
                AddBoundary(node, scan, used, siblingNames, errors);
                return;
            }

            var propertyName = HierarchyBindingsNaming.ToIdentifier(scan.Child.name);

            if (Validate(propertyName, scan.Child.name, scan.ChildPath, errors) == false)
                return;

            propertyName = HierarchyBindingsNaming.MakeUnique(propertyName, used);

            // The nested class name lives in the same namespace as the properties,
            // so it is also deduplicated through the shared set of taken names.
            scan.Node.TypeName = HierarchyBindingsNaming.MakeUnique(propertyName + "Bindings", used);
            scan.Node.PropertyName = propertyName;
            scan.Node.FieldName = HierarchyBindingsNaming.ToFieldName(propertyName);
            node.Children.Add(scan.Node);
        }

        // The name comes from the object, not the type: from outside it is a child like the others,
        // its own bindings class is just responsible for its insides. A child with several bindings gets a
        // field per class: the object name + the type name without the Bindings suffix (Pair + ABindings =
        // PairA). Such a name is not deduplicated with a digit: a taken one is a generation error.
        private static void AddBoundary(
            HierarchyBindingsNode node,
            ChildScan scan,
            HashSet<string> used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            if (scan.Boundaries.Count == 1)
            {
                var single = MakeField(scan.Boundaries[0], scan.Child.name, used, errors, scan.ChildPath);

                if (single == null)
                    return;

                single.Comment = scan.ChildPath + " (own bindings)";
                node.Fields.Add(single);
                return;
            }

            foreach (var boundary in scan.Boundaries)
            {
                var propertyName = HierarchyBindingsNaming.ToIdentifier(scan.Child.name) + ToShortTypeName(boundary);

                if (ReserveExact(propertyName, scan.ChildPath, used, siblingNames, errors) == false)
                    continue;

                node.Fields.Add(new HierarchyBindingsField
                {
                    Target = boundary,
                    PropertyName = propertyName,
                    FieldName = HierarchyBindingsNaming.ToFieldName(propertyName),
                    CodeTypeName = ToCodeTypeName(boundary),
                    Comment = scan.ChildPath + " (own bindings)"
                });
            }
        }

        private static string ToShortTypeName(Component boundary)
        {
            var name = boundary.GetType().Name;
            var suffix = HierarchyBindingsGenerator.TypeNameSuffix;

            return name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)
                ? name.Substring(0, name.Length - suffix.Length)
                : name;
        }

        // The field name for one of several bindings of a child must not match a taken name nor the name of a
        // sibling that gets its field later: otherwise the sibling would silently become "Name2".
        private static bool ReserveExact(
            string propertyName,
            string hierarchyPath,
            HashSet<string> used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            if (Validate(propertyName, propertyName, hierarchyPath, errors) == false)
                return false;

            if (siblingNames.Contains(propertyName) || used.Add(propertyName) == false)
            {
                errors.Add(
                    $"'{hierarchyPath}': field '{propertyName}' for one of its bindings collides with another member. " +
                    "Rename the object or the bindings class.");
                return false;
            }

            return true;
        }

        // A row of siblings with their own bindings - an array of references per type. The set of types is the
        // same for every element (the signature checked it), but the order of the components may differ, so an
        // element is found by type, not by position.
        private static void AddBoundaryGroup(
            HierarchyBindingsNode node,
            List<ChildScan> scans,
            int start,
            int length,
            string baseName,
            string comment,
            HashSet<string> used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            var first = scans[start];
            var isSingle = first.Boundaries.Count == 1;

            foreach (var template in first.Boundaries)
            {
                var type = template.GetType();
                var plural = HierarchyBindingsNaming.ToPlural(baseName);
                string propertyName;

                if (isSingle)
                {
                    propertyName = HierarchyBindingsNaming.MakeUnique(plural, used);
                }
                else
                {
                    propertyName = plural + ToShortTypeName(template);

                    if (ReserveExact(propertyName, first.ChildPath, used, siblingNames, errors) == false)
                        return;
                }

                var field = new HierarchyBindingsField
                {
                    Target = template,
                    PropertyName = propertyName,
                    FieldName = HierarchyBindingsNaming.ToFieldName(propertyName),
                    CodeTypeName = ToCodeTypeName(template),
                    Comment = comment + " (own bindings)"
                };

                for (var index = start; index < start + length; index++)
                {
                    var element = scans[index].Boundaries.FirstOrDefault(candidate => candidate.GetType() == type);

                    if (element == null)
                    {
                        errors.Add($"'{scans[index].ChildPath}' has no {type.Name}, but its group expects it.");
                        return;
                    }

                    field.Targets.Add(element);
                }

                node.Fields.Add(field);
            }
        }

        // A row collapses only when the siblings carry the same set of bindings types. The order of the
        // components on the objects is not part of the signature.
        private static string ComputeBoundarySignature(List<Component> boundaries)
        {
            var types = boundaries
                .Select(boundary => ToCodeTypeName(boundary))
                .OrderBy(name => name, StringComparer.Ordinal);

            return "boundary:" + string.Join(",", types);
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

        // Node contents without its own name: decides whether one class serves
        // all siblings at once.
        private static string ComputeSignature(HierarchyBindingsNode node)
        {
            var builder = new StringBuilder();
            AppendSignature(builder, node);
            return builder.ToString();
        }

        private static void AppendSignature(StringBuilder builder, HierarchyBindingsNode node)
        {
            foreach (var field in node.Fields)
                builder.Append(field.PropertyName).Append(':').Append(field.DeclaredTypeName).Append(';');

            foreach (var child in node.Children)
            {
                builder.Append(child.PropertyName).Append(child.IsArray ? "[" + child.Elements.Count + "]" : "");
                builder.Append('{');
                AppendSignature(builder, child);
                builder.Append('}');
            }
        }

        private sealed class ChildScan
        {
            public GameObject Child;
            public string ChildPath;
            public string BaseName;
            public string Signature;

            // The child has its own bindings: do not go inside, expose a reference to each of its bindings
            // components. Null - a regular child, then Node is set.
            public List<Component> Boundaries;
            public HierarchyBindingsNode Node;
        }

        // Only bindings of the own layer are a boundary: on a variant the base bindings on an inherited child are
        // foreign components, and the walk goes on through such an object.
        private static List<Component> FindBindings(
            GameObject target,
            string hierarchyPath,
            HierarchyBindingsLayer layer,
            List<string> errors)
        {
            var result = new List<Component>();

            foreach (var component in target.GetComponents<Component>())
            {
                if (component is IObjectBindings == false)
                    continue;

                if (layer.IsOwn(component) == false)
                    continue;

                if (result.Any(existing => existing.GetType() == component.GetType()))
                {
                    errors.Add($"'{hierarchyPath}' carries {component.GetType().Name} twice. Remove the duplicate.");
                    continue;
                }

                result.Add(component);
            }

            return result;
        }

        private static HierarchyBindingsField MakeField(
            Component component,
            string rawName,
            HashSet<string> used,
            List<string> errors,
            string hierarchyPath)
        {
            var propertyName = HierarchyBindingsNaming.ToIdentifier(rawName);

            if (Validate(propertyName, rawName, hierarchyPath, errors) == false)
                return null;

            propertyName = HierarchyBindingsNaming.MakeUnique(propertyName, used);

            return new HierarchyBindingsField
            {
                Target = component,
                PropertyName = propertyName,
                FieldName = HierarchyBindingsNaming.ToFieldName(propertyName),
                CodeTypeName = ToCodeTypeName(component)
            };
        }

        private static bool Validate(string propertyName, string rawName, string hierarchyPath, List<string> errors)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                errors.Add($"'{hierarchyPath}': name '{rawName}' is not a C# identifier.");
                return false;
            }

            if (HierarchyBindingsNaming.IsReservedMember(propertyName))
            {
                errors.Add(
                    $"'{hierarchyPath}': '{propertyName}' collides with a MonoBehaviour member. Rename the object.");
                return false;
            }

            return true;
        }

        private static bool IsPredefinedAssembly(Type type)
        {
            var name = type.Assembly.GetName().Name;
            return name == "Assembly-CSharp" || name == "Assembly-CSharp-firstpass";
        }

        public static string ToCodeTypeName(Type type)
        {
            var fullName = type.FullName ?? type.Name;
            return "global::" + fullName.Replace('+', '.');
        }

        // Component types are read from the script sources while the last compile failed (see ScriptTypeNames).
        private static string ToCodeTypeName(Component component)
        {
            var fullName = ScriptTypeNames.FullName(component.GetType(), ScriptTypeNames.ScriptOf(component));
            return "global::" + fullName.Replace('+', '.');
        }
    }
}