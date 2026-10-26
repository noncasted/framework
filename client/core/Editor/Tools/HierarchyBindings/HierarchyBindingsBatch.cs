using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        private const string PlanKey = "Internal.HierarchyBindings.PrefabBatch";

        public static bool IsRunning => LoadPlan() != null;

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
            var items = new List<PrefabItem>();

            foreach (var path in paths)
                CollectPrefab(path, items);

            var plan = new PrefabPlan { Assets = paths };

            foreach (var group in items
                         .GroupBy(item => (item.VariantDepth, item.HierarchyDepth))
                         .OrderBy(group => group.Key.VariantDepth)
                         .ThenByDescending(group => group.Key.HierarchyDepth))
            {
                var phase = new PrefabPhase();
                phase.Requests.AddRange(group.Select(item => item.Request));
                plan.Phases.Add(phase);
            }

            if (ValidateUniqueTypes(items) == false)
                return "Duplicate bindings types. See the console.";

            SavePlan(plan);
            Debug.Log($"[{LogTag}] Prefab batch: {items.Count} bindings in {paths.Count} prefabs, " +
                      $"{plan.Phases.Count} phases.");
            RunNextPhase();
            return Status();
        }

        // The state for MCP: the caller pumps HierarchyBindingsGenerator.PumpPending while the batch runs.
        public static string Status()
        {
            var plan = LoadPlan();

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
            Debug.LogWarning($"[{LogTag}] Prefab batch cancelled.");
        }

        // A step of the batch threw: nothing pending may wait for a pass that will not come.
        internal static void Abort()
        {
            ClearPlan();
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

        // Every binding declared in prefab assets and in loaded scenes, in one pass without phases (Regenerate
        // All). The bindings are already on the objects, so the "children before parents" order is not needed,
        // and all the code is written before one Refresh - otherwise after a failed compile a phase would wait
        // for a reload that never comes. Only bindings whose code changed (or whose object is stale) are bound
        // again. Scenes that are not loaded are not opened (opening scenes from tools crashes the shared
        // editor): they are listed in the log. The caller flushes once (changed = a file was written).
        public static int PrepareProject(out bool changed)
        {
            if (IsRunning)
                throw new InvalidOperationException("A prefab batch is running. Pump it to the end or cancel it first.");

            if (HierarchyBindingsQueue.IsEmpty() == false)
                throw new InvalidOperationException("Bindings queue is not empty. Pump pending requests first.");

            RequireNoPrefabStage();

            var items = new List<PrefabItem>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (asset == null)
                    throw new InvalidOperationException($"{path} is not a prefab.");

                if (asset.GetComponentsInChildren<ObjectBindings>(true).Length > 0)
                    CollectPrefab(path, items);
            }

            if (ValidateUniqueTypes(items) == false)
                throw new InvalidOperationException("Duplicate bindings types. See the console.");

            var types = new HashSet<string>(items.Select(item => item.FullTypeName), StringComparer.Ordinal);
            var files = new HashSet<string>(items.Select(item => GeneratedFilePath(item.Request)),
                StringComparer.Ordinal);
            var prepared = 0;
            var failed = 0;
            changed = false;

            foreach (var item in items)
            {
                var request = item.Request;

                if (HierarchyBindingsGenerator.Prepare(FindInAsset(request), request.TypeName, request.Namespace,
                        request.IsSceneService, request.IsEntityComponent, out var fileChanged, false) == false)
                {
                    failed++;
                    continue;
                }

                prepared++;
                changed |= fileChanged;
            }

            var loadedScenes = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (scene.isLoaded == false)
                    continue;

                loadedScenes.Add(scene.path);

                foreach (var component in Collect(scene))
                {
                    // Bindings of prefab instances are regenerated in their prefabs above.
                    if (HierarchyBindingsLayer.ResolveDeclaringAsset(component, scene.path) != scene.path)
                        continue;

                    var type = HierarchyBindingsGenerator.ResolveGeneratedType(component);

                    if (type == null)
                        throw new InvalidOperationException(
                            $"{component.GetType().Name} in {scene.path} does not derive from a generated class.");

                    if (types.Add(type.FullName) == false)
                        throw new InvalidOperationException(
                            $"'{type.FullName}' in {scene.path} is used by more than one object.");

                    var file = HierarchyBindingsGenerator.ToFilePath(
                        HierarchyBindingsPaths.ResolveGeneratedFolder(component.gameObject, scene.path), type.Name);

                    if (files.Add(file) == false)
                        throw new InvalidOperationException(
                            $"'{type.FullName}' in {scene.path} is generated into {file}, which another class uses.");

                    if (HierarchyBindingsGenerator.Prepare(component.gameObject, type.Name, type.Namespace ?? string.Empty,
                            component.IsSceneService, component.IsEntityComponent, out var fileChanged, false) == false)
                    {
                        failed++;
                        continue;
                    }

                    prepared++;
                    changed |= fileChanged;
                }
            }

            LogSkippedScenes(loadedScenes);

            if (failed > 0)
                throw new InvalidOperationException($"{failed} bindings failed to regenerate. See the console.");

            return prepared;
        }

        public static int Count(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded ? Collect(scene).Count : 0;
        }

        // Called by the generator after every binding pass.
        internal static void OnPendingProcessed()
        {
            if (IsRunning)
                RunNextPhase();
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
            var plan = LoadPlan();

            while (plan.Next < plan.Phases.Count)
            {
                var phase = plan.Phases[plan.Next];
                plan.Next++;
                SavePlan(plan);

                var changed = false;
                var prepared = 0;

                foreach (var request in phase.Requests)
                {
                    var target = FindInAsset(request);

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

                Debug.Log($"[{LogTag}] Prefab batch: phase {plan.Next} of {plan.Phases.Count}, " +
                          $"{prepared} bindings{(changed ? ", compiling" : string.Empty)}.");
                HierarchyBindingsGenerator.Flush(changed);
                return;
            }

            Finish(plan);
        }

        // The last step: the entity view lists in every prefab of the batch (the base bindings get into the
        // views of the variants only this way) and the check of the result.
        private static void Finish(PrefabPlan plan)
        {
            ClearPlan();

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
            var message = $"[{LogTag}] Prefab batch done: {plan.Assets.Count} prefabs, " +
                          $"{synced} entity view lists updated, {errors} validation errors.";

            if (errors > 0)
                Debug.LogError(message);
            else
                Debug.Log(message);
        }

        internal static void CollectPrefab(string assetPath, List<PrefabItem> items)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (asset == null)
                throw new InvalidOperationException($"{assetPath} is not a prefab.");

            var variantDepth = VariantDepth(asset);
            var contents = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                foreach (var bindings in contents.GetComponentsInChildren<ObjectBindings>(true))
                {
                    if (HierarchyBindingsLayer.ResolveDeclaringAsset(bindings, assetPath) != assetPath)
                        continue;

                    var type = HierarchyBindingsGenerator.ResolveGeneratedType(bindings);

                    if (type == null)
                        throw new InvalidOperationException(
                            $"{bindings.GetType().Name} in {assetPath} does not derive from a generated class.");

                    items.Add(new PrefabItem
                    {
                        VariantDepth = variantDepth,
                        HierarchyDepth = Depth(bindings.transform),
                        FullTypeName = type.FullName,
                        Request = new HierarchyBindingsRequest
                        {
                            AssetPath = assetPath,
                            ObjectPath = HierarchyBindingsTarget.IndexPath(bindings.transform, contents.transform),
                            ObjectNames = HierarchyBindingsTarget.NamePath(bindings.gameObject),
                            TypeName = type.Name,
                            Namespace = type.Namespace ?? string.Empty,
                            IsPrefabAsset = true,
                            IsSceneService = bindings.IsSceneService,
                            IsEntityComponent = bindings.IsEntityComponent
                        }
                    });
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // One class - one object: otherwise two owners would overwrite the file one after another. One file - one
        // class too: the file is named by the short type name, so ShopBindings of two namespaces generated into one
        // folder would overwrite each other.
        internal static bool ValidateUniqueTypes(List<PrefabItem> items)
        {
            var valid = true;

            foreach (var group in items.GroupBy(item => item.FullTypeName))
            {
                if (group.Count() == 1)
                    continue;

                valid = false;
                Debug.LogError($"[{LogTag}] '{group.Key}' is declared on more than one object: {Places(group)}.");
            }

            foreach (var group in items.GroupBy(item => GeneratedFilePath(item.Request)))
            {
                var types = group.Select(item => item.FullTypeName).Distinct(StringComparer.Ordinal).ToList();

                if (types.Count == 1)
                    continue;

                valid = false;
                Debug.LogError(
                    $"[{LogTag}] {string.Join(", ", types)} are all generated into {group.Key}: {Places(group)}. " +
                    "Rename all of them but one.");
            }

            return valid;
        }

        private static string Places(IEnumerable<PrefabItem> items)
        {
            return string.Join(", ", items.Select(item => item.Request.AssetPath + ":" + item.Request.ObjectNames));
        }

        private static string GeneratedFilePath(HierarchyBindingsRequest request)
        {
            var folder = HierarchyBindingsPaths.ResolveGeneratedFolder(FindInAsset(request), request.AssetPath);
            return HierarchyBindingsGenerator.ToFilePath(folder, request.TypeName);
        }

        private static int VariantDepth(GameObject asset)
        {
            var depth = 0;

            for (var current = asset;
                 PrefabUtility.GetPrefabAssetType(current) == PrefabAssetType.Variant;
                 current = PrefabUtility.GetCorrespondingObjectFromSource(current))
                depth++;

            return depth;
        }

        private static int Depth(Transform target)
        {
            var depth = 0;

            for (var current = target.parent; current != null; current = current.parent)
                depth++;

            return depth;
        }

        internal static GameObject FindInAsset(HierarchyBindingsRequest request)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(request.AssetPath);

            if (asset == null)
                throw new InvalidOperationException($"{request.AssetPath} disappeared during the batch.");

            var found = HierarchyBindingsTarget.FindByIndexPath(asset.transform, request.ObjectPath);

            if (found == null)
                throw new InvalidOperationException(
                    $"'{request.ObjectNames}' disappeared from {request.AssetPath} during the batch.");

            return found.gameObject;
        }

        private static PrefabPlan LoadPlan()
        {
            var json = SessionState.GetString(PlanKey, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PrefabPlan>(json);
        }

        private static void SavePlan(PrefabPlan plan)
        {
            SessionState.SetString(PlanKey, JsonUtility.ToJson(plan));
        }

        private static void ClearPlan()
        {
            SessionState.EraseString(PlanKey);
        }

        private static List<ObjectBindings> Collect(Scene scene)
        {
            var result = new List<ObjectBindings>();

            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<ObjectBindings>(true));

            return result;
        }

        private static void LogSkippedScenes(HashSet<string> loadedScenes)
        {
            var scriptGuids = CollectBindingsScriptGuids();
            var skipped = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (loadedScenes.Contains(path) || ReferencesAny(path, scriptGuids) == false)
                    continue;

                skipped.Add(path);
            }

            if (skipped.Count == 0)
                return;

            Debug.Log(
                $"[{LogTag}] Not loaded scenes with bindings were skipped (open each and use Project Tools > " +
                $"Current Scene > Regenerate All Bindings): {string.Join(", ", skipped)}");
        }

        private static List<string> CollectBindingsScriptGuids()
        {
            var result = new List<string>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<ObjectBindings>())
            {
                foreach (var guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));

                    if (script != null && script.GetClass() == type)
                        result.Add(guid);
                }
            }

            return result;
        }

        private static bool ReferencesAny(string scenePath, List<string> scriptGuids)
        {
            var text = File.ReadAllText(CatalogPaths.ToFullPath(scenePath));

            foreach (var guid in scriptGuids)
            {
                if (text.Contains("guid: " + guid, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        internal sealed class PrefabItem
        {
            public int VariantDepth;
            public int HierarchyDepth;
            public string FullTypeName;
            public HierarchyBindingsRequest Request;
        }

        [Serializable]
        private sealed class PrefabPlan
        {
            public List<string> Assets = new();
            public List<PrefabPhase> Phases = new();
            public int Next;
        }

        [Serializable]
        private sealed class PrefabPhase
        {
            public List<HierarchyBindingsRequest> Requests = new();
        }
    }
}
