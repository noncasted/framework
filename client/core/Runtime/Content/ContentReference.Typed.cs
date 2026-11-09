using System;
using Unity.Loading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // A lazy reference to an asset of type T of the content directory: the owner is loaded without its target, which
    // comes through IContentLoader on a lifetime. Drawn in the inspector as an object field of T (ContentReferenceDrawer).
    // Target is the loaded object of this very reference: the loader counts the lifetimes of each reference and
    // releases its Loadable when the last one ends; two references to one asset are counted by the engine and give
    // one object.
    [Serializable]
    public sealed class ContentReference<T> : ContentReference where T : Object
    {
        public ContentReference(LoadableObjectId id, string guid, long localId) : base(guid, localId)
        {
            _loadable = new Loadable<T>(id);
        }

        [SerializeField] private Loadable<T> _loadable;

        public override LoadableObjectId Id => _loadable.LoadableObjectId;

        public bool IsLoaded => _loadable.Status == LoadableStatus.Loaded;

        public T Target
        {
            get
            {
                if (IsLoaded == false)
                    throw new InvalidOperationException($"[Content] {typeof(T).Name} {this} is not loaded ({_loadable.Status})");

                return _loadable.Target;
            }
        }

        internal Loadable<T> Loadable => _loadable;

#if UNITY_EDITOR
        // The asset behind the id, for tools and mocks.
        public T EditorAsset => (T)UnityEditor.LoadableObjectIdEditorUtility.LoadableObjectIdToObject(Id);
#endif
    }
}
