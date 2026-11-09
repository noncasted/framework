using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Internal
{
    // The json manifest of a built content directory: <hash>.json, named by BuildManifestHash.txt next to it. The engine
    // does not promise the format (UnityDataTools, Documentation/contentdirectory-format.md), so this is the only class
    // that reads it, and anything it does not expect throws.
    public sealed class ContentManifest
    {
        private ContentManifest(string folder, string hash, string name, IReadOnlyList<int> roots,
            IReadOnlyList<ContentManifestScene> scenes, IReadOnlyList<ContentManifestFile> serializedFiles)
        {
            Folder = folder;
            Hash = hash;
            Name = name;
            Roots = roots;
            Scenes = scenes;
            SerializedFiles = serializedFiles;
        }

        public const string HashFile = "BuildManifestHash.txt";

        private const int SupportedVersion = 3;

        public string Folder { get; }
        public string Hash { get; }
        public string Name { get; }
        public string FileName => $"{Hash}.json";

        // The serialized files of the root assets.
        public IReadOnlyList<int> Roots { get; }

        public IReadOnlyList<ContentManifestScene> Scenes { get; }

        // Indexed by ContentManifestFile.Index.
        public IReadOnlyList<ContentManifestFile> SerializedFiles { get; }

        public static ContentManifest Read(string folder)
        {
            var hash = File.ReadAllText(Path.Combine(folder, HashFile)).Trim();

            if (hash.Length == 0)
                throw new InvalidDataException($"Empty {HashFile} in {folder}");

            var path = Path.Combine(folder, $"{hash}.json");
            var data = JsonUtility.FromJson<Data>(File.ReadAllText(path));

            if (data == null)
                throw new InvalidDataException($"Empty content directory manifest {path}");

            if (data.Version != SupportedVersion)
                throw new InvalidDataException($"The content directory manifest {path} has the version {data.Version}, only {SupportedVersion} is known");

            if (data.BuildInfo == null || string.IsNullOrEmpty(data.BuildInfo.BuildName))
                throw new InvalidDataException($"The content directory manifest {path} has no build name");

            var files = ReadFiles(data, path);
            var roots = ReadRoots(data, files.Length, path);
            var scenes = ReadScenes(data, files.Length, path);

            return new ContentManifest(folder, hash, data.BuildInfo.BuildName, roots, scenes, files);
        }

        private static ContentManifestFile[] ReadFiles(Data data, string path)
        {
            if (data.Guids == null || data.Guids.Length == 0 || data.SerializedFiles == null || data.SerializedFiles.Length == 0)
                throw new InvalidDataException($"The content directory manifest {path} has no GUIDs or serialized files");

            var files = new ContentManifestFile[data.SerializedFiles.Length];

            for (var i = 0; i < files.Length; i++)
            {
                var entry = data.SerializedFiles[i];

                if (entry.Index != i)
                    throw new InvalidDataException($"The serialized file {i} of {path} has the index {entry.Index}");

                if (entry.Id == null || entry.Id.Guid < 0 || entry.Id.Guid >= data.Guids.Length)
                    throw new InvalidDataException($"The serialized file {i} of {path} points at no GUID");

                if (Enum.IsDefined(typeof(ContentFileKind), entry.Id.Kind) == false)
                    throw new InvalidDataException($"The serialized file {i} of {path} has an unknown kind {entry.Id.Kind}");

                var kind = (ContentFileKind)entry.Id.Kind;
                var hash = string.IsNullOrEmpty(entry.ContentHash) ? null : entry.ContentHash;

                if ((hash == null) != (kind == ContentFileKind.DefaultResources))
                    throw new InvalidDataException($"The serialized file {i} of {path} of the kind {kind} has the content hash '{hash}'");

                var dependencies = entry.SerializedFileDependencies ?? Array.Empty<int>();

                foreach (var dependency in dependencies)
                {
                    if (dependency < 0 || dependency >= data.SerializedFiles.Length)
                        throw new InvalidDataException($"The serialized file {i} of {path} depends on the missing file {dependency}");
                }

                files[i] = new ContentManifestFile(i, data.Guids[entry.Id.Guid], kind, entry.Id.Lfid, hash, dependencies);
            }

            return files;
        }

        private static int[] ReadRoots(Data data, int fileCount, string path)
        {
            if (data.RootAssets == null || data.RootAssets.Length == 0)
                throw new InvalidDataException($"The content directory manifest {path} has no root assets");

            var roots = new int[data.RootAssets.Length];

            for (var i = 0; i < roots.Length; i++)
                roots[i] = CheckFile(data.RootAssets[i].SerializedFile, fileCount, $"the root {i}", path);

            return roots;
        }

        private static ContentManifestScene[] ReadScenes(Data data, int fileCount, string path)
        {
            var entries = data.LoadableScenes ?? Array.Empty<Scene>();
            var scenes = new ContentManifestScene[entries.Length];

            for (var i = 0; i < scenes.Length; i++)
            {
                var entry = entries[i];

                if (string.IsNullOrEmpty(entry.GUID))
                    throw new InvalidDataException($"The scene {entry.Path} of {path} has no GUID");

                scenes[i] = new ContentManifestScene(entry.GUID, entry.Path, CheckFile(entry.SerializedFile, fileCount, $"the scene {entry.Path}", path));
            }

            return scenes;
        }

        private static int CheckFile(int file, int fileCount, string owner, string path)
        {
            if (file < 0 || file >= fileCount)
                throw new InvalidDataException($"{owner} of {path} points at the missing serialized file {file}");

            return file;
        }

        // The fields of the format this class reads; the names are the format.
        [Serializable]
        private sealed class Data
        {
            public int Version;
            public BuildInfo BuildInfo;
            public Root[] RootAssets;
            public Scene[] LoadableScenes;
            public string[] Guids;
            public SerializedFile[] SerializedFiles;
        }

        [Serializable]
        private sealed class BuildInfo
        {
            public string BuildName;
        }

        [Serializable]
        private sealed class Root
        {
            public int SerializedFile;
        }

        [Serializable]
        private sealed class Scene
        {
            public string GUID;
            public string Path;
            public int SerializedFile;
        }

        [Serializable]
        private sealed class SerializedFile
        {
            public int Index;
            public FileId Id;
            public int[] SerializedFileDependencies;
            public string ContentHash;
        }

        [Serializable]
        private sealed class FileId
        {
            public int Guid;
            public long Lfid;
            public int Kind;
        }
    }
}
