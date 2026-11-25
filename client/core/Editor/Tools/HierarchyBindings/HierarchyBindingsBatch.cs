using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Internal
{
    // Batch regeneration: the code of all bindings is written before the domain reload, and binding
    // happens after it from the shared request queue.
    //
    // Prefabs go in phases, with compilation and binding between the phases. The order: the base before the
    // variants (by the depth of the variant chain), children before parents inside an asset. A parent is
    // generated from the bindings already on its children, so a new binding of a child must be bound before
    // the parent is scanned. A phase without code changes does not compile and is bound right away.
    // The plan of the phases lives in SessionState: the domain reloads between the phases.
    public static class HierarchyBindingsBatch
    {
        private const string LogTag = "HierarchyBindingsGenerator";

        private const string ResultTemplate =
            "Prefab batch done: {Prefabs} prefabs, {Synced} entity view lists updated, {Errors} validation errors.";

        private static readonly ILog _log = Logs.Of(typeof(HierarchyBindingsBatch));

        public static bool IsRunning => HierarchyBindingsPrefabPlans.Load() != null;

        // Regenerates every binding declared in these prefabs (bindings of the base on a variant and
        // bindings of nested prefabs are not theirs, they are regenerated in their own assets if those are
        // in the list too). Returns a line for the log / MCP; the progress and the result go to the console.
        public static string RegeneratePrefabs(IEnumerable<string> assetPaths)
        {
            if (IsRunning)
                throw new InvalidOperationException("A prefab batch is already running. Pump it to the end first.");

            if (HierarchyBindingsQueue.IsEmpty() == false)
                throw new InvalidOperationException("Bindings queue is not empty. Pump pending requests first.");

            RequireNoPrefabStage();

            var paths = assetPaths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            var items = new List<HierarchyBindingsPrefabItem>();

            foreach (var path in paths)
                HierarchyBindingsPrefabItems.CollectPrefab(path, items);

            var plan = HierarchyBindingsPrefabPlans.Build(paths, items);

            if (HierarchyBindingsPrefabItems.ValidateUniqueTypes(items) == false)
                return "Duplicate bindings types. See the console.";

            HierarchyBindingsPrefabPlans.Save(plan);
            _log.Info("Prefab batch: {Bindings} bindings in {Prefabs} prefabs, {Phases} phases.", items.Count, paths.Count,
                plan.Phases.Count);
            RunNextPhase();
            return Status();
        }

        // The state for MCP: the caller pumps HierarchyBindingsGenerator.PumpPending while the batch runs.
        public static string Status()
        {
            var plan = HierarchyBindingsPrefabPlans.Load();

            if (plan == null)
                return "Idle.";

            return $"Phase {plan.Next} of {plan.Phases.Count} is binding" +
                   (EditorApplication.isCompiling ? " (compiling)." : ".");
        }

        // The batch got stuck (the generated code failed to compile, a manual edit between phases): drop the
        // plan and the requests. Phases already bound stay as they are - a rerun passes them without changes.
        public static void Cancel()
        {
            Abort();
            _log.Warning("Prefab batch cancelled.");
        }

        // A step of the batch threw: nothing pending may wait for a pass that will not come.
        internal static void Abort()
        {
            HierarchyBindingsPrefabPlans.Clear();
            HierarchyBindingsGenerator.ResetPending();
        }

        // The batch plans from the prefabs on the disk, and the generator scans and binds a prefab open in a prefab
        // stage in the stage: one batch would mix the unsaved copy with the asset, and the entity views written at the
        // end would be overwritten by the stage on save.
        private static void RequireNoPrefabStage()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage != null)
                throw new InvalidOperationException(
                    $"[{LogTag}] {stage.assetPath} is open in a prefab stage. Save it and close the stage first.");
        }

        public static string RegenerateScene(Scene scene)
        {
            return HierarchyBindingsSceneBatch.RegenerateScene(scene);
        }

        // Every binding declared in prefab assets and in loaded scenes (Regenerate All, see HierarchyBindingsProjectBatch).
        // The caller flushes once (changed = a file was written); the pass that follows ends with the validation of
        // every prefab with bindings and of the loaded scenes, its findings are errors in the console.
        public static int PrepareProject(out bool changed)
        {
            if (IsRunning)
                throw new InvalidOperationException("A prefab batch is running. Pump it to the end or cancel it first.");

            if (HierarchyBindingsQueue.IsEmpty() == false)
                throw new InvalidOperationException("Bindings queue is not empty. Pump pending requests first.");

            RequireNoPrefabStage();

            var prepared = HierarchyBindingsProjectBatch.Prepare(out changed);

            // The validator runs after the binding pass the caller flushes (HierarchyBindingsProjectValidation).
            HierarchyBindingsProjectValidation.Request();
            return prepared;
        }

        public static int Count(Scene scene)
        {
            return HierarchyBindingsSceneBatch.Count(scene);
        }

        // Called by the generator after every binding pass.
        internal static void OnPendingProcessed()
        {
            if (IsRunning)
                RunNextPhase();
            else
                HierarchyBindingsProjectValidation.RunIfRequested();
        }

        // A phase that throws (a prefab that cannot be found or scanned) ends the batch: its plan already points past
        // the phase, and nothing would ever be bound to move it on.
        private static void RunNextPhase()
        {
            try
            {
                RunPhases();
            }
            catch (Exception)
            {
                Abort();
                throw;
            }
        }

        private static void RunPhases()
        {
            var plan = HierarchyBindingsPrefabPlans.Load();

            while (plan.Next < plan.Phases.Count)
            {
                var phase = plan.Phases[plan.Next];
                plan.Next++;
                HierarchyBindingsPrefabPlans.Save(plan);

                var changed = false;
                var prepared = 0;

                foreach (var request in phase.Requests)
                {
                    var target = HierarchyBindingsPrefabItems.FindInAsset(request);

                    if (HierarchyBindingsGenerator.Prepare(target, request.TypeName, request.Namespace,
                            request.IsSceneService, request.IsEntityComponent, out var fileChanged) == false)
                        continue;

                    prepared++;
                    changed |= fileChanged;
                }

                // The whole phase failed - nothing to bind, and a second run will not save it: move on,
                // the errors are already in the console.
                if (prepared == 0)
                    continue;

                _log.Info("Prefab batch: phase {Phase} of {Phases}, {Bindings} bindings{Compiling}.", plan.Next,
                    plan.Phases.Count, prepared, changed ? ", compiling" : string.Empty);
                HierarchyBindingsGenerator.Flush(changed);
                return;
            }

            Finish(plan);
        }

        // The last step: the entity view lists in every prefab of the batch (the base bindings get into the
        // views of the variants only this way) and the check of the result.
        private static void Finish(HierarchyBindingsPrefabPlan plan)
        {
            HierarchyBindingsPrefabPlans.Clear();

            var synced = 0;

            foreach (var path in plan.Assets)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    if (HierarchyBindingsEntityViews.SyncAll(contents) == false)
                        continue;

                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    synced++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            if (synced > 0)
                AssetDatabase.SaveAssets();

            var errors = HierarchyBindingsValidator.ValidatePrefabs(plan.Assets);
            // An error when the validation found something.
            if (errors > 0)
                _log.Error(ResultTemplate, plan.Assets.Count, synced, errors);
            else
                _log.Info(ResultTemplate, plan.Assets.Count, synced, errors);
        }
    }
}
