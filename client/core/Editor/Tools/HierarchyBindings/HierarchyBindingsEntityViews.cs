using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Entity bindings get into the entity scope only through ScopeEntityView._autoDetected: the same list
    // is read by ContainerGraphAssetScanner when it writes the container asset map. The view rebuilds the
    // list in OnValidate, which is not called on loaded prefab contents, so after binding we sync it
    // ourselves - by the same rule as the Scan button.
    //
    // A view may sit only on the variants of a prefab, not on the base: the bindings of the base get into
    // the list of every variant when that variant is synced.
    internal static class HierarchyBindingsEntityViews
    {
        private const string FieldName = "_autoDetected";

        // Every view under the root (batch regeneration: the whole prefab). Returns true if any list changed.
        public static bool SyncAll(GameObject root)
        {
            var changed = false;

            foreach (var view in root.GetComponentsInChildren<ScopeEntityView>(true))
                changed |= SyncView(view);

            return changed;
        }

        // Only the views above the bindings carrier: it is their lists it gets into. Other views of the scene
        // are not touched, so binding one object does not edit half of the scene.
        public static bool SyncOwners(GameObject bindingsCarrier)
        {
            var changed = false;

            foreach (var view in bindingsCarrier.GetComponentsInParent<ScopeEntityView>(true))
                changed |= SyncView(view);

            return changed;
        }

        // Scene bindings get into the scene scope through SceneServicesFactory._services - the same list
        // "Assets/Scan services" rebuilds. We rebuild it ourselves after binding in a scene.
        public static bool SyncSceneServices(UnityEngine.SceneManagement.Scene scene)
        {
            var changed = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var listener in root.GetComponentsInChildren<ISceneReloadListener>(true))
                {
                    if (listener.OnReload() == false)
                        continue;

                    EditorUtility.SetDirty((MonoBehaviour)listener);
                    changed = true;
                }
            }

            return changed;
        }

        public static bool IsSceneServiceListed(UnityEngine.SceneManagement.Scene scene, MonoBehaviour service)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var factory in root.GetComponentsInChildren<SceneServicesFactory>(true))
                {
                    var property = new SerializedObject(factory).FindProperty("_services");

                    if (property == null || property.isArray == false)
                        throw new InvalidOperationException(
                            $"SceneServicesFactory on '{factory.name}' has no serialized list '_services'.");

                    for (var index = 0; index < property.arraySize; index++)
                    {
                        if (property.GetArrayElementAtIndex(index).objectReferenceValue == service)
                            return true;
                    }
                }
            }

            return false;
        }

        // The list the view must hold: every IEntityComponent under it, as in OnValidate.
        public static List<MonoBehaviour> Expected(ScopeEntityView view)
        {
            var result = new List<MonoBehaviour>();

            foreach (var component in view.GetComponentsInChildren<IEntityComponent>(true))
            {
                if (component is not MonoBehaviour behaviour)
                    throw new InvalidOperationException(
                        $"{component.GetType().Name} implements IEntityComponent but is not a MonoBehaviour.");

                result.Add(behaviour);
            }

            return result;
        }

        public static List<UnityEngine.Object> Current(ScopeEntityView view)
        {
            var property = FindList(new SerializedObject(view), view);
            var result = new List<UnityEngine.Object>(property.arraySize);

            for (var index = 0; index < property.arraySize; index++)
                result.Add(property.GetArrayElementAtIndex(index).objectReferenceValue);

            return result;
        }

        private static bool SyncView(ScopeEntityView view)
        {
            var expected = Expected(view);
            var serializedObject = new SerializedObject(view);
            var property = FindList(serializedObject, view);

            if (Matches(property, expected))
                return false;

            property.arraySize = expected.Count;

            for (var index = 0; index < expected.Count; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = expected[index];

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            return true;
        }

        private static bool Matches(SerializedProperty property, List<MonoBehaviour> expected)
        {
            if (property.arraySize != expected.Count)
                return false;

            for (var index = 0; index < expected.Count; index++)
            {
                if (property.GetArrayElementAtIndex(index).objectReferenceValue != expected[index])
                    return false;
            }

            return true;
        }

        private static SerializedProperty FindList(SerializedObject serializedObject, ScopeEntityView view)
        {
            var property = serializedObject.FindProperty(FieldName);

            if (property == null || property.isArray == false)
                throw new InvalidOperationException(
                    $"{view.GetType().Name} on '{view.name}' has no serialized list '{FieldName}'.");

            return property;
        }
    }
}
