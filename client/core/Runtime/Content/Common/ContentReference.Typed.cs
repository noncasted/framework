using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // A lazy reference to an asset of type T of a content bundle: the owner is loaded without its target, which comes
    // through IContentLoader on a lifetime. Drawn in the inspector as an object field of T (ContentReferenceDrawer).
    // Target is the loaded object of this very reference: the loader counts the lifetimes of each reference and lets the
    // bundle go when the last one ends; two references to one asset give one object of the bundle.
    [Serializable]
    public sealed class ContentReference<T> : ContentReference where T : Object
    {
        public ContentReference(string guid, long localId) : base(guid, localId)
        {
        }

        [NonSerialized] private T _target;

        // A destroyed object (its bundle unloaded) is no target either.
        public bool IsLoaded => _target != null;

        public T Target
        {
            get
            {
                if (IsLoaded == false)
                    throw new InvalidOperationException($"[Content] {typeof(T).Name} {this} is not loaded");

                return _target;
            }
        }

        internal void Fill(T target)
        {
            _target = target;
        }

        internal void Clear()
        {
            _target = null;
        }

#if UNITY_EDITOR
        // The asset behind the GUID and the local id, for tools and mocks.
        public T EditorAsset => IsEmpty ? null : ContentEditorAssets.Find(Guid, LocalId) as T;
#endif
    }
}
