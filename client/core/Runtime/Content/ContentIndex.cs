using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Internal
{
    // The files of a built content directory and the files each of its loads needs, computed by the build from the
    // manifest and baked into the player next to it. The store fetches the manifest and the root files before the
    // registration, writes the rest as zeros of their size, and fetches the files of a load before the load.
    // Objects are keyed by the GUID of their asset and their local id, not by LoadableObjectId: its fields are internal
    // at runtime, and a LoadableObjectId baked into an asset of the player is rewritten by the build.
    [Serializable]
    public sealed class ContentIndex
    {
        public ContentIndex(
            string name,
            string manifestHash,
            ContentFile[] files,
            int manifest,
            int[] rootFiles,
            ContentObjectFiles[] objects,
            ContentSceneFiles[] scenes,
            bool isPreloaded,
            bool isBrotli)
        {
            _name = name;
            _manifestHash = manifestHash;
            _files = files;
            _manifest = manifest;
            _rootFiles = rootFiles;
            _objects = objects;
            _scenes = scenes;
            _isPreloaded = isPreloaded;
            _isBrotli = isBrotli;
            Validate();
        }

        // For JsonUtility.
        private ContentIndex()
        {
        }

        [SerializeField] private string _name;
        [SerializeField] private string _manifestHash;
        [SerializeField] private ContentFile[] _files;
        [SerializeField] private int _manifest;
        [SerializeField] private int[] _rootFiles;
        [SerializeField] private ContentObjectFiles[] _objects;
        [SerializeField] private ContentSceneFiles[] _scenes;
        [SerializeField] private bool _isPreloaded;
        [SerializeField] private bool _isBrotli;

        [NonSerialized] private Dictionary<(string, long), ContentFile[]> _objectLookup;
        [NonSerialized] private Dictionary<string, ContentFile[]> _sceneLookup;

        // The name of the directory: the folder the store registers, StreamingAssets/Content/<name>.
        public string Name => _name;

        // The content of BuildManifestHash.txt, which the store writes itself: the only file not named by its hash.
        public string ManifestHash => _manifestHash;

        // The files are in the .data file of a WebGL build, read without a request.
        public bool IsPreloaded => _isPreloaded;

        // The files are shipped compressed as <name>.br (a WebGL build with brotli): the server sends them with
        // Content-Encoding: br and the browser hands over the bytes of the file. The sizes of Files are the real ones.
        public bool IsBrotli => _isBrotli;

        // What the name of a file gets in the URL it is fetched from.
        public string ShippedSuffix => _isBrotli ? ContentPaths.BrotliExtension : string.Empty;

        public IReadOnlyList<ContentFile> Files => _files;
        public ContentFile Manifest => _files[_manifest];
        public IReadOnlyList<ContentObjectFiles> Objects => _objects;
        public IReadOnlyList<ContentSceneFiles> Scenes => _scenes;

        // What the registration opens: the closure of the dependencies of the root files, without the manifest.
        public IReadOnlyList<ContentFile> RootFiles => Resolve(_rootFiles);

        public static ContentIndex FromJson(string json)
        {
            var index = JsonUtility.FromJson<ContentIndex>(json);

            if (index == null)
                throw new InvalidDataException("The json of the content index is empty");

            index.Validate();
            return index;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        // An object of an imported asset has a file of its own; any other object is in the file of its whole asset.
        public IReadOnlyList<ContentFile> ObjectFiles(string guid, long localId)
        {
            _objectLookup ??= BuildObjectLookup();

            if (_objectLookup.TryGetValue((guid, localId), out var files))
                return files;

            if (_objectLookup.TryGetValue((guid, 0), out files))
                return files;

            throw new KeyNotFoundException($"The content directory {_name} has no object {guid}:{localId}");
        }

        public IReadOnlyList<ContentFile> SceneFiles(string guid)
        {
            _sceneLookup ??= BuildSceneLookup();

            if (_sceneLookup.TryGetValue(guid, out var files))
                return files;

            throw new KeyNotFoundException($"The content directory {_name} has no scene {guid}");
        }

        private Dictionary<(string, long), ContentFile[]> BuildObjectLookup()
        {
            var lookup = new Dictionary<(string, long), ContentFile[]>();

            foreach (var entry in _objects)
                lookup.Add((entry.Guid, entry.LocalId), Resolve(entry.Files));

            return lookup;
        }

        private Dictionary<string, ContentFile[]> BuildSceneLookup()
        {
            var lookup = new Dictionary<string, ContentFile[]>();

            foreach (var entry in _scenes)
                lookup.Add(entry.Guid, Resolve(entry.Files));

            return lookup;
        }

        private ContentFile[] Resolve(IReadOnlyList<int> indices)
        {
            var files = new ContentFile[indices.Count];

            for (var i = 0; i < indices.Count; i++)
                files[i] = _files[indices[i]];

            return files;
        }

        private void Validate()
        {
            if (string.IsNullOrEmpty(_name))
                throw new InvalidDataException("The content index has no name");

            if (string.IsNullOrEmpty(_manifestHash))
                throw new InvalidDataException($"The content index {_name} has no manifest hash");

            if (_files == null || _files.Length == 0)
                throw new InvalidDataException($"The content index {_name} has no files");

            if (_rootFiles == null || _objects == null || _scenes == null)
                throw new InvalidDataException($"The content index {_name} misses its roots, objects or scenes");

            if (_isPreloaded && _isBrotli)
                throw new InvalidDataException($"The content index {_name} is preloaded and brotli at once: the .data file is compressed whole");

            CheckIndex(_manifest, "the manifest");

            if (_files[_manifest].Name != $"{_manifestHash}.json")
                throw new InvalidDataException($"The manifest of the content index {_name} is {_files[_manifest].Name}, not {_manifestHash}.json");

            var names = new HashSet<string>();

            foreach (var file in _files)
            {
                if (string.IsNullOrEmpty(file.Name) || file.Size < 0)
                    throw new InvalidDataException($"The content index {_name} has a broken file {file}");

                if (names.Add(file.Name) == false)
                    throw new InvalidDataException($"The content index {_name} lists the file {file.Name} twice");
            }

            foreach (var file in _rootFiles)
                CheckIndex(file, "a root file");

            var objects = new HashSet<(string, long)>();

            foreach (var entry in _objects)
            {
                if (objects.Add((entry.Guid, entry.LocalId)) == false)
                    throw new InvalidDataException($"The content index {_name} lists the object {entry.Guid}:{entry.LocalId} twice");

                CheckFiles(entry.Files, $"the object {entry.Guid}:{entry.LocalId}");
            }

            var scenes = new HashSet<string>();

            foreach (var entry in _scenes)
            {
                if (scenes.Add(entry.Guid) == false)
                    throw new InvalidDataException($"The content index {_name} lists the scene {entry.Path} twice");

                CheckFiles(entry.Files, $"the scene {entry.Path}");
            }
        }

        private void CheckFiles(IReadOnlyList<int> files, string owner)
        {
            if (files == null || files.Count == 0)
                throw new InvalidDataException($"The content index {_name} has no files of {owner}");

            foreach (var file in files)
                CheckIndex(file, $"a file of {owner}");
        }

        private void CheckIndex(int index, string what)
        {
            if (index < 0 || index >= _files.Length)
                throw new InvalidDataException($"The content index {_name} points {what} at the file {index} of {_files.Length}");
        }
    }
}
