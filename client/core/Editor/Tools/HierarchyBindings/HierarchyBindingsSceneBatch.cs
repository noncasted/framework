using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // The bindings of one loaded scene: all the classes are written first, then one refresh.
    internal static class HierarchyBindingsSceneBatch
    {
        private const string LogTag = HierarchyBindingsLog.Tag;

        public static string RegenerateScene(Scene scene)
        {
            if (scene.IsValid() == false || scene.isLoaded == false)
                return "No loaded scene.";

            var bindings = Collect(scene);

            if (bindings.Count == 0)
                return $"No bindings found in '{scene.name}'.";

            var types = new HashSet<string>(StringComparer.Ordinal);
            var prepared = 0;
            var foreign = 0;
            var changed = false;

            foreach (var component in bindings)
            {
                // Bindings of prefab instances are declared in the prefabs themselves and are regenerated there:
                // from the scene they would be written as overrides of the instance.
                if (HierarchyBindingsLayer.ResolveDeclaringAsset(component, scene.path) != scene.path)
                {
                    foreign++;
                    continue;
                }

                var type = HierarchyBindingsGenerator.ResolveGeneratedType(component);

                if (type == null)
                {
                    Debug.LogError($"[{LogTag}] Failed to resolve the generated type.", component);
                    continue;
                }

                // One class - one object: two owners of the same type would overwrite the file one after
                // another, and the last one would win.
                if (types.Add(type.FullName) == false)
                {
                    Debug.LogError($"[{LogTag}] '{type.FullName}' is used by more than one object. Skipped.",
                        component);
                    continue;
                }

                var ok = HierarchyBindingsGenerator.Prepare(
                        component.gameObject,
                        type.Name,
                        type.Namespace ?? string.Empty,
                        component.IsSceneService,
                        component.IsEntityComponent,
                        out var fileChanged
                    );

                if (ok == false)
                    continue;

                prepared++;
                changed |= fileChanged;
            }

            var foreignNote = foreign > 0 ? $" Skipped {foreign} bindings declared in prefabs." : string.Empty;

            if (prepared == 0)
                return $"Nothing regenerated in '{scene.name}'. See the console.{foreignNote}";

            HierarchyBindingsGenerator.Flush(changed);
            return $"Regenerating {prepared} bindings in '{scene.name}'...{foreignNote}";
        }

        public static int Count(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded ? Collect(scene).Count : 0;
        }

        public static List<ObjectBindings> Collect(Scene scene)
        {
            var result = new List<ObjectBindings>();

            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<ObjectBindings>(true));

            return result;
        }
    }
}
