using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The files a load of one scene of a content directory needs (the GUID of the scene asset, as LoadableSceneId
    // prints it), as indices into ContentIndex.Files.
    [Serializable]
    public struct ContentSceneFiles
    {
        public ContentSceneFiles(string guid, string path, int[] files)
        {
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException("A content scene needs the GUID of its asset", nameof(guid));

            if (files.Length == 0)
                throw new ArgumentException($"The content scene {path} has no files", nameof(files));

            _guid = guid;
            _path = path;
            _files = files;
        }

        [SerializeField] private string _guid;
        [SerializeField] private string _path;
        [SerializeField] private int[] _files;

        public string Guid => _guid;
        public string Path => _path;

        // The file of the scene first, then its dependencies and the resources of each file.
        public IReadOnlyList<int> Files => _files;
    }
}
