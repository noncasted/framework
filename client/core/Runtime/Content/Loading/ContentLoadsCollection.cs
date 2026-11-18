using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Internal
{
    // The running loads and the progress of every reference, shared by the loaders of the bundles of one root scope: a
    // reference loaded through two loaders is one load, so its Target is one object. Main thread only.
    internal sealed class ContentLoadsCollection
    {
        private readonly Dictionary<ContentReference, object> _objects = new();
        private readonly Dictionary<string, ViewableProperty<ContentProgress>> _progress = new();

        public bool TryGet<T>(ContentReference<T> reference, out ContentObjectLoad<T> load) where T : Object
        {
            if (_objects.TryGetValue(reference, out var running) == false)
            {
                load = null;
                return false;
            }

            load = (ContentObjectLoad<T>)running;
            return true;
        }

        public void Add<T>(ContentReference<T> reference, ContentObjectLoad<T> load) where T : Object
        {
            _objects.Add(reference, load);
        }

        // The load is over: the next load of the reference starts anew.
        public void Remove(ContentReference reference)
        {
            _objects.Remove(reference);
        }

        // The progress of the key, created by the first ask and kept as long as the collection.
        public ViewableProperty<ContentProgress> Progress(string key, Func<ContentProgress> create)
        {
            if (_progress.TryGetValue(key, out var progress))
                return progress;

            progress = new ViewableProperty<ContentProgress>(create());
            _progress.Add(key, progress);
            return progress;
        }
    }
}
