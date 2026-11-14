using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Internal
{
    // The archive of a built content directory and the files each of its loads needs, computed by the build from the
    // manifest and shipped next to the archive as <hash>.index.json (ContentPaths.IndexFile), read once at the start.
    // A fetching store writes the archive as zeros of its length, fetches the header, the manifest and the root files
    // before the registration and the spans of the files of a load before the load. The files are in the order of the
    // archive, their spans inside [HeaderLength, ArchiveLength) and apart.
    // Objects are keyed by the GUID of their asset and their local id, not by LoadableObjectId: its fields are internal
    // at runtime, and a LoadableObjectId baked into an asset of the player is rewritten by the build.
    [Serializable]
    public sealed class ContentIndex
    {
        // The name of the header among the files a store fetches; no file of a directory is named so.
        public const string HeaderName = "header";

        public ContentIndex(
            string name,
            string manifestHash,
            long archiveLength,
            long headerLength,
            ContentFile[] files,
            int manifest,
            int[] rootFiles,
            ContentObjectFiles[] objects,
            ContentSceneFiles[] scenes)
        {
            _name = name;
            _manifestHash = manifestHash;
            _archiveLength = archiveLength;
            _headerLength = headerLength;
            _files = files;
            _manifest = manifest;
            _rootFiles = rootFiles;
            _objects = objects;
            _scenes = scenes;
            Validate();
        }

        // For JsonUtility.
        private ContentIndex()
        {
        }

        [SerializeField] private string _name;
        [SerializeField] private string _manifestHash;
        [SerializeField] private long _archiveLength;
        [SerializeField] private long _headerLength;
        [SerializeField] private ContentFile[] _files;
        [SerializeField] private int _manifest;
        [SerializeField] private int[] _rootFiles;
        [SerializeField] private ContentObjectFiles[] _objects;
        [SerializeField] private ContentSceneFiles[] _scenes;

        [NonSerialized] private Dictionary<(string, long), ContentFile[]> _objectLookup;
        [NonSerialized] private Dictionary<string, ContentFile[]> _sceneLookup;

        // The name of the directory: the folder the store registers, StreamingAssets/Content/<name>.
        public string Name => _name;

        // The content of BuildManifestHash.txt, which the store writes itself: the only file not named by its hash.
        public string ManifestHash => _manifestHash;

        // The bytes of the archive (ContentPaths.ArchiveFile).
        public long ArchiveLength => _archiveLength;

        // The header and the table of contents of the archive, what the registration reads on the mount.
        public ContentFile Header => new(HeaderName, 0, _headerLength);

        public IReadOnlyList<ContentFile> Files => _files;
        public ContentFile Manifest => _files[_manifest];
        public IReadOnlyList<ContentObjectFiles> Objects => _objects;
        public IReadOnlyList<ContentSceneFiles> Scenes => _scenes;

        // What the registration opens: the closure of the dependencies of the root files, without the manifest.
        public IReadOnlyList<ContentFile> RootFiles => Resolve(_rootFiles);

        // The index of a directory preloaded into the .data file of a WebGL build: a TextAsset of Resources next to the stamp,
        // read without a request.
        public static ContentIndex FromResource(string name)
        {
            var asset = Resources.Load<TextAsset>(ContentPaths.IndexResource(name));

            if (asset == null)
                throw new InvalidOperationException($"[Content] No index of the preloaded content directory {name} in Resources/{ContentPaths.IndexResource(name)}");

            var index = FromJson(asset.text);
            Resources.UnloadAsset(asset);
            return index;
        }

        public static ContentIndex FromJson(string json)
        {
            using var span = GameProfiler.Scope("Content: index");
            var index = JsonUtility.FromJson<ContentIndex>(json);

            if (index == null)
                throw new InvalidDataException("The json of the content index is empty");

            index.Validate();
            return index;
        }

        // Without indentation: the index of a game has thousands of entries (every file and object of the directory), and the
        // indentation was 60% of its bytes, read at every start.
        public string ToJson()
        {
            return JsonUtility.ToJson(this);
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

            if (_headerLength <= 0 || _archiveLength <= _headerLength)
                throw new InvalidDataException($"The content index {_name} has an archive of {_archiveLength} bytes with a header of {_headerLength}");

            CheckIndex(_manifest, "the manifest");

            if (_files[_manifest].Name != $"{_manifestHash}.json")
                throw new InvalidDataException($"The manifest of the content index {_name} is {_files[_manifest].Name}, not {_manifestHash}.json");

            var names = new HashSet<string>();
            var end = _headerLength;

            foreach (var file in _files)
            {
                if (string.IsNullOrEmpty(file.Name) || file.Name == HeaderName || file.Size <= 0)
                    throw new InvalidDataException($"The content index {_name} has a broken file {file}");

                if (names.Add(file.Name) == false)
                    throw new InvalidDataException($"The content index {_name} lists the file {file.Name} twice");

                if (file.Offset < end || file.End > _archiveLength)
                    throw new InvalidDataException($"The content index {_name} puts {file} before the end {end} of the file before it or past the archive of {_archiveLength} bytes");

                end = file.End;
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
