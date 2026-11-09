using System;
using System.IO;
using UnityEngine;

namespace Internal
{
    // What a player build knows of its content directory before it reads the index: the hash of the build (the names of
    // the index and of the fetched archive), whether the directory is in the .data file of a WebGL build, and whether
    // its index is shipped compressed with brotli (ContentPaths.BrotliExtension). The json TextAsset
    // Resources/Content/<name>, written by the content build.
    [Serializable]
    public sealed class ContentStamp
    {
        public ContentStamp(string manifestHash, bool isPreloaded, bool isIndexBrotli)
        {
            _manifestHash = manifestHash;
            _isPreloaded = isPreloaded;
            _isIndexBrotli = isIndexBrotli;
            Validate();
        }

        // For JsonUtility.
        private ContentStamp()
        {
        }

        [SerializeField] private string _manifestHash;
        [SerializeField] private bool _isPreloaded;
        [SerializeField] private bool _isIndexBrotli;

        public string ManifestHash => _manifestHash;

        // The archive is in the .data file of a WebGL build, read without a request.
        public bool IsPreloaded => _isPreloaded;

        // The server sends the index with Content-Encoding: br, the browser decodes it.
        public bool IsIndexBrotli => _isIndexBrotli;

        // The name of the index in the folder of the directory as shipped.
        public string ShippedIndex => ContentPaths.IndexFile(_manifestHash) + (_isIndexBrotli ? ContentPaths.BrotliExtension : string.Empty);

        public static ContentStamp Load(string name)
        {
            var asset = Resources.Load<TextAsset>(ContentPaths.StampResource(name));

            if (asset == null)
                throw new InvalidOperationException($"[Content] No stamp of the content directory {name} in Resources/{ContentPaths.StampResource(name)}");

            return FromJson(asset.text);
        }

        public static ContentStamp FromJson(string json)
        {
            var stamp = JsonUtility.FromJson<ContentStamp>(json);

            if (stamp == null)
                throw new InvalidDataException("The json of the content stamp is empty");

            stamp.Validate();
            return stamp;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        // The index must be of the build the player was made with.
        public ContentIndex Check(ContentIndex index)
        {
            if (index.ManifestHash != _manifestHash)
                throw new InvalidDataException($"[Content] The index of {index.Name} is of the build {index.ManifestHash}, the player was built with {_manifestHash}");

            return index;
        }

        private void Validate()
        {
            if (string.IsNullOrEmpty(_manifestHash))
                throw new InvalidDataException("The content stamp has no manifest hash");

            if (_isPreloaded && _isIndexBrotli)
                throw new InvalidDataException($"The content stamp {_manifestHash} is preloaded and brotli at once: a preloaded directory ships its index raw");
        }
    }
}
