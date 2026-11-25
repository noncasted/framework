using System.Diagnostics;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Internal
{
    // The check that ends Regenerate All: every prefab with bindings and every loaded scene goes through the validator
    // once the binding pass is over (the pass runs after the reload, so the request lives in SessionState). Regenerate
    // All binds only what changed, and a variant that broke the bindings of its base declares nothing to regenerate:
    // without the check such bindings stay broken until Play.
    internal static class HierarchyBindingsProjectValidation
    {
        private const string RequestedKey = "Internal.HierarchyBindings.ProjectValidation";

        private const string ResultTemplate =
            "Regenerate All: {Prefabs} prefabs and {Scenes} scenes validated in {Ms} ms, {Errors} validation errors.";

        private static readonly ILog _log = Logs.Of(typeof(HierarchyBindingsProjectValidation));

        public static bool IsRequested => SessionState.GetBool(RequestedKey, false);

        public static void Request()
        {
            SessionState.SetBool(RequestedKey, true);
        }

        public static void Clear()
        {
            SessionState.EraseBool(RequestedKey);
        }

        // Called after every binding pass, an empty one too.
        public static void RunIfRequested()
        {
            if (IsRequested == false)
                return;

            Clear();

            var watch = Stopwatch.StartNew();
            var prefabs = HierarchyBindingsProjectBatch.PrefabsWithBindings();
            var errors = HierarchyBindingsValidator.ValidatePrefabs(prefabs);
            var scenes = 0;

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (scene.isLoaded == false || HierarchyBindingsSceneBatch.Count(scene) == 0)
                    continue;

                errors += HierarchyBindingsValidator.ValidateScene(scene);
                scenes++;
            }

            // An error when the validation found something.
            if (errors > 0)
                _log.Error(ResultTemplate, prefabs.Count, scenes, watch.ElapsedMilliseconds, errors);
            else
                _log.Info(ResultTemplate, prefabs.Count, scenes, watch.ElapsedMilliseconds, errors);
        }
    }
}
