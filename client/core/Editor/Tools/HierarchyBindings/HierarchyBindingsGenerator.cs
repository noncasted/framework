using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Internal
{
    // Generation runs in two phases: first we write the class, then, after compilation, attach it
    // to the object and fill the references. The request lives in SessionState between the phases
    // because the domain reloads in between.
    public static class HierarchyBindingsGenerator
    {
        public const string TypeNameSuffix = HierarchyBindingsGeneratedTypes.TypeNameSuffix;

        private const string LogTag = "HierarchyBindingsGenerator";
        private const string AwaitingReloadKey = "Internal.HierarchyBindings.AwaitingReload";

        private static readonly ILog _log = Logs.Of(typeof(HierarchyBindingsGenerator));

        public static string ToTypeName(string bindingsName)
        {
            return HierarchyBindingsGeneratedTypes.ToTypeName(bindingsName);
        }

        public static void Regenerate(ObjectBindings bindings)
        {
            var type = ResolveGeneratedType(bindings);

            if (type == null)
            {
                Debug.LogError($"[{LogTag}] Failed to resolve the generated type.", bindings);
                return;
            }

            Generate(
                    bindings.gameObject,
                    type.Name,
                    type.Namespace ?? string.Empty,
                    bindings.IsSceneService,
                    bindings.IsEntityComponent
                );
        }

        public static Type ResolveGeneratedType(Component bindings)
        {
            return HierarchyBindingsGeneratedTypes.ResolveGeneratedType(bindings);
        }

        public static void Generate(
            GameObject root,
            string bindingsName,
            string namespaceName,
            bool isSceneService,
            bool isEntityComponent)
        {
            if (Prepare(root, bindingsName, namespaceName, isSceneService, isEntityComponent, out var changed) == false)
                return;

            Flush(changed);
        }

        // See HierarchyBindingsPreparation: code is written separately from triggering compilation.
        public static bool Prepare(
            GameObject root,
            string bindingsName,
            string namespaceName,
            bool isSceneService,
            bool isEntityComponent,
            out bool changed,
            bool bindUnchanged = true)
        {
            return HierarchyBindingsPreparation.Prepare(root, bindingsName, namespaceName, isSceneService, isEntityComponent,
                out changed, bindUnchanged);
        }

        // Without code changes there is no compilation, so DidReloadScripts will not fire either,
        // hence we bind right away. With changes we set the "awaiting reload" mark: until the reload the
        // requests must not be bound - the domain still has the old classes without the new fields.
        public static void Flush(bool changed)
        {
            if (changed)
            {
                SessionState.SetBool(AwaitingReloadKey, true);
                AssetDatabase.Refresh();
                return;
            }

            EditorApplication.delayCall += ProcessPending;
        }

        // The entry point for MCP and an unfocused editor: there delayCall does not tick, and the binding
        // after compilation does not start by itself. Safe to call any number of times - while compiling or
        // awaiting the reload it does nothing. Returns the state.
        public static string PumpPending()
        {
            if (EditorApplication.isCompiling || SessionState.GetBool(AwaitingReloadKey, false))
                return "Waiting for compilation. " + HierarchyBindingsBatch.Status();

            ProcessPending();

            if (HierarchyBindingsQueue.IsEmpty() == false)
                return "Pending requests remain. " + HierarchyBindingsBatch.Status();

            return HierarchyBindingsBatch.Status();
        }

        // Requests, the awaiting mark or the validation of Regenerate All are left (for example, the generated code
        // failed to compile and the reload never came): only a reset (Cancel) moves on.
        internal static bool HasPending()
        {
            return HierarchyBindingsQueue.HasData() || SessionState.GetBool(AwaitingReloadKey, false) ||
                   HierarchyBindingsProjectValidation.IsRequested;
        }

        // Resets a stuck state (for example, the generated code failed to compile): the queue and the awaiting mark.
        internal static void ResetPending()
        {
            HierarchyBindingsQueue.Clear();
            SessionState.EraseBool(AwaitingReloadKey);
            HierarchyBindingsProjectValidation.Clear();
        }

        [InitializeOnLoadMethod]
        private static void InstallRegenerateHandler()
        {
            ObjectBindings.SetRegenerateHandler(Regenerate);
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            SessionState.EraseBool(AwaitingReloadKey);

            if (HierarchyBindingsQueue.IsEmpty() && HierarchyBindingsProjectValidation.IsRequested == false)
                return;

            EditorApplication.delayCall += ProcessPending;
        }

        private static void ProcessPending()
        {
            // An old delayCall may outlive a manual pump and arrive when the next phase of the batch
            // has already written its code and awaits compilation.
            if (EditorApplication.isCompiling || SessionState.GetBool(AwaitingReloadKey, false))
                return;

            var requests = HierarchyBindingsQueue.Load();

            // Nothing to bind: only the validation of Regenerate All may wait for this pass.
            if (requests.Count == 0)
            {
                HierarchyBindingsProjectValidation.RunIfRequested();
                return;
            }

            var bound = 0;

            // The queue is consumed only after the pass: a request that throws must not take the rest with it
            // silently. An exception ends the batch and drops what is pending, loudly: the next pass would hit it
            // again, and a batch waiting for this pass would never move on.
            try
            {
                foreach (var request in requests)
                {
                    if (HierarchyBindingsRequestProcessor.Process(request))
                        bound++;
                }
            }
            catch (Exception exception)
            {
                HierarchyBindingsBatch.Abort();
                throw new InvalidOperationException(
                    $"[{LogTag}] Binding failed, the pending bindings and the prefab batch are reset: " +
                    exception.Message, exception);
            }

            HierarchyBindingsQueue.Clear();

            if (bound > 0)
            {
                AssetDatabase.SaveAssets();
                _log.Info("Bound {Bound} of {Requests} bindings.", bound, requests.Count);
            }

            HierarchyBindingsBatch.OnPendingProcessed();
        }
    }
}
