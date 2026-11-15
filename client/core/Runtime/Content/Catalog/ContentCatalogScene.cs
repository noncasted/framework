using System;
using UnityEngine;

namespace Internal
{
    // A scene of the catalog: its GUID, its bundle and its path, the name the engine loads it by.
    [Serializable]
    public sealed class ContentCatalogScene
    {
        public ContentCatalogScene(string guid, string bundle, string path)
        {
            _guid = guid;
            _bundle = bundle;
            _path = path;
        }

        [SerializeField] private string _guid;
        [SerializeField] private string _bundle;
        [SerializeField] private string _path;

        public string Guid => _guid;
        public string Bundle => _bundle;
        public string Path => _path;
    }
}
