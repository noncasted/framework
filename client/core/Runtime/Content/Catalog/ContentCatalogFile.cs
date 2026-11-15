using System;
using UnityEngine;

namespace Internal
{
    // A file of a content bundle under StreamingAssets/Content: named by its hash (<bundle>.<hash>.bundle), so a server and
    // the Cache API keep it forever. Size: the bytes of the bundle (the progress of a fetch); the scenes of a bundle have
    // a file of their own.
    [Serializable]
    public sealed class ContentCatalogFile
    {
        public ContentCatalogFile(string name, long size, bool isScenes)
        {
            _name = name;
            _size = size;
            _isScenes = isScenes;
        }

        [SerializeField] private string _name;
        [SerializeField] private long _size;
        [SerializeField] private bool _isScenes;

        public string Name => _name;
        public long Size => _size;
        public bool IsScenes => _isScenes;
    }
}
