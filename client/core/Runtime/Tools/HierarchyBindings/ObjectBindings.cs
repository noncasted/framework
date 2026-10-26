using UnityEngine;
#if UNITY_EDITOR
using System;
using Unity.Scripting.LifecycleManagement;
#endif

namespace Internal
{
    // Common base of all generated bindings. It exists not for behaviour but for addressing:
    // it is used to find the generated class itself when a user class inherits it,
    // and the regenerate button lives on it.
#if UNITY_EDITOR
    [NoAutoStaticsCleanup]
#endif
    public abstract class ObjectBindings : MonoBehaviour, IObjectBindings
    {
        // Snapshot of the hierarchy at generation time. Lives in the base so the generated class
        // consists of references only.
        [SerializeField, HideInInspector] private string _structureHash;

        // Layer - GUID of the asset the bindings are declared in (scene, prefab or variant). A variant carries the
        // bindings of its base with the layer of the base next to its own ones, and each class mirrors only the
        // objects and components of its layer.
        [SerializeField, HideInInspector] private string _layerAssetGuid;

        // Not every binding needs DI registration, so interfaces are generated according to these
        // checkboxes. The value is stored on the component so Regenerate knows what to write.
        [SerializeField] private bool _isSceneService;
        [SerializeField] private bool _isEntityComponent;

        public string StructureHash => _structureHash;

        public string LayerAssetGuid => _layerAssetGuid;

        public bool IsSceneService => _isSceneService;

        public bool IsEntityComponent => _isEntityComponent;

#if UNITY_EDITOR
        // The runtime assembly cannot reference the editor one, so the link is one-way:
        // the generator assigns itself here on domain load.
        public static Action<ObjectBindings> RegenerateHandler;

        [Button("Regenerate")]
        private void Regenerate()
        {
            if (RegenerateHandler == null)
            {
                Debug.LogError("[HierarchyBindingsGenerator] Generator is not loaded.", this);
                return;
            }

            RegenerateHandler.Invoke(this);
        }
#endif
    }
}