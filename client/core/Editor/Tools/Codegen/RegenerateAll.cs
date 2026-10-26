using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Runs every editor generator once, in dependency order, synchronously:
    //   1. inputs without type names: colors, scenes, sprites, audio;
    //   2. hierarchy bindings (code of prefab assets and loaded scenes);
    //   3. catalogs that spell type names: env assets, prefabs;
    //   4. steps of the project ([RegenerateAllStep] in its editor assemblies, by name): generators of the game that
    //      write scripts or addressable entries the next steps read;
    //   5. container asset map (views and bindings of prefabs and scenes, read by the container generator);
    //   6. shared addressables (after every catalog group exists);
    // then one refresh: changed scripts recompile and the Roslyn container generator runs inside that compile.
    // Bindings whose code changed are bound after the reload. Colors run only in a project with a ColorCatalog asset:
    // the catalogue is optional, and its generator throws without one.
    //
    // Works after a failed compile too: the editor keeps the previous domain and its menus, and the generators read
    // type names from the script sources while the last compile failed (ScriptTypeNames). That unblocks a namespace
    // rename and code that already references a not yet generated catalog member.
    public static class RegenerateAll
    {
        private const string LogTag = "RegenerateAll";

        [MenuItem(FrameworkMenu.Codegen + "Regenerate All", priority = FrameworkMenu.GroupPriority)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException($"[{LogTag}] Exit Play mode first.");

            if (EditorApplication.isCompiling)
                throw new InvalidOperationException($"[{LogTag}] Scripts are compiling, run it after the reload.");

            if (ScriptTypeNames.IsDomainStale)
                Debug.LogWarning(
                    $"[{LogTag}] The last compile failed: the previous domain is running, type names are read from the script sources.");

            if (ColorGenerator.HasCatalog())
                Step("Colors", ColorGenerator.GenerateNow);
            else
                Debug.Log($"[{LogTag}] Colors skipped: the project has no ColorCatalog asset.");

            Step("Scenes", SceneGenerator.Generate);
            Step("Sprites", SpriteGenerator.GenerateNow);
            Step("Audio", AudioCatalogGenerator.GenerateNow);

            var bindingsChanged = false;
            var bindings = 0;

            // The bindings written before a later step throws are flushed anyway: their requests are queued, and
            // without the flush they would wait for a pass that never comes and block the next batch.
            try
            {
                Step("Hierarchy bindings", () => bindings = HierarchyBindingsBatch.PrepareProject(out bindingsChanged));

                Step("Env assets", AssetCatalogGenerator.GenerateNow);
                Step("Prefabs", PrefabCatalogGenerator.GenerateNow);

                foreach (var step in ProjectSteps())
                    Step(step.Name, step.Run);

                Step("Container asset map", ContainerGraphPostprocessor.GenerateNow);
                Step("Shared addressables", SharedAddressablesSync.GenerateNow);
            }
            finally
            {
                // Refresh covers every written script; binding of changed bindings waits for the reload.
                HierarchyBindingsGenerator.Flush(bindingsChanged);
                AssetDatabase.Refresh();
            }

            Debug.Log($"[{LogTag}] Done: {bindings} bindings checked. Changed scripts recompile now.");
        }

        // The steps of the project ([RegenerateAllStep]) in the order Regenerate All runs them.
        public static IReadOnlyList<ProjectStep> ProjectSteps()
        {
            var steps = new List<ProjectStep>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<RegenerateAllStepAttribute>())
            {
                if (method.IsStatic == false || method.ReturnType != typeof(void) || method.GetParameters().Length != 0)
                    throw new InvalidOperationException(
                        $"[{LogTag}] {method.DeclaringType.FullName}.{method.Name}: a step must be a static void method without parameters");

                var attribute = method.GetCustomAttribute<RegenerateAllStepAttribute>();
                steps.Add(new ProjectStep(attribute.Name, (Action)method.CreateDelegate(typeof(Action))));
            }

            steps.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return steps;
        }

        private static void Step(string name, Action action)
        {
            Debug.Log($"[{LogTag}] {name}");
            action();
        }

        public readonly struct ProjectStep
        {
            public ProjectStep(string name, Action run)
            {
                Name = name;
                Run = run;
            }

            public string Name { get; }

            // Runs the generator without a refresh, like Regenerate All does.
            public Action Run { get; }
        }
    }
}
