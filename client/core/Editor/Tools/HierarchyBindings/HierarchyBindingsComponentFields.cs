using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // The fields of one object in the mirror: the object itself, its components, the bindings on a child.
    [NoAutoStaticsCleanup]
    internal static class HierarchyBindingsComponentFields
    {
        private static readonly HashSet<Type> SkippedComponents = new()
        {
            typeof(CanvasRenderer),
            typeof(HierarchyBindingsIgnoreChildren)
        };

        // Everyone needs the object itself: nested classes have no other path to it, and for the root
        // the property just duplicates MonoBehaviour.gameObject for uniform access.
        public static void CollectGameObject(HierarchyBindingsNode node, IdentifierScope used)
        {
            var propertyName = HierarchyBindingsMemberNames.DeclareMember("GameObject", used);

            node.Fields.Add(new HierarchyBindingsField(
                node.Target,
                propertyName,
                HierarchyBindingsNaming.ToFieldName(propertyName),
                "global::UnityEngine.GameObject"));
        }

        public static void CollectComponents(
            HierarchyBindingsNode node,
            IdentifierScope used,
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

        // Only bindings of the own layer are a boundary: on a variant the base bindings on an inherited child are
        // foreign components, and the walk goes on through such an object.
        public static List<Component> FindBindings(
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

        public static HierarchyBindingsField MakeField(
            Component component,
            string rawName,
            IdentifierScope used,
            List<string> errors,
            string hierarchyPath)
        {
            var propertyName = HierarchyBindingsNaming.ToIdentifier(rawName);

            if (HierarchyBindingsMemberNames.Validate(propertyName, rawName, hierarchyPath, used, errors) == false)
                return null;

            propertyName = HierarchyBindingsMemberNames.DeclareMember(propertyName, used);

            return new HierarchyBindingsField(
                component,
                propertyName,
                HierarchyBindingsNaming.ToFieldName(propertyName),
                HierarchyBindingsMemberNames.ToCodeTypeName(component));
        }

        private static bool IsPredefinedAssembly(Type type)
        {
            var name = type.Assembly.GetName().Name;
            return name == "Assembly-CSharp" || name == "Assembly-CSharp-firstpass";
        }
    }
}
