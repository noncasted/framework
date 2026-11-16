using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Internal
{
    // The layout of the project the framework editor tools work in: where generated code and catalogue data go, which
    // folders hold game modules and scenes. Read from ProjectSettings/Framework.json, which every project has: the
    // framework knows no folders of a game. ContentAssembly and ContentFolder are required; a missing list is empty
    // and a missing folder is none. The file is read again when it changes.
    public static class FrameworkSettings
    {
        public const string SettingsPath = "ProjectSettings/Framework.json";
        public const string CorePackage = "Packages/com.noncasted.framework.core";

        private static Data _data;
        private static DateTime _dataWriteTime;
        private static long _dataLength;

        // The project assembly that owns the generated catalogues (InternalAssets, Sprites, Scenes, ...), the asset map
        // of the container and the root scope. Namespace Internal, like the framework core.
        public static string ContentAssembly => Get().ContentAssembly;

        // The folder of ContentAssembly: Runtime/ (generated code, catalogue groups, options) and Editor/ (catalogue
        // group lists).
        public static string ContentFolder => Get().ContentFolder;

        // Game modules that keep their runtime assets in an Assets/ subfolder (Scenes, Prefabs, Generated, Options).
        public static IReadOnlyList<string> ModuleRoots => Get().ModuleRoots;

        // Folders searched for scenes of the Scenes catalogue.
        public static IReadOnlyList<string> SceneFolders => Get().SceneFolders;

        // Root of the art: sprite groups and metadata are named by the path under it.
        public static string ArtFolder => Get().ArtFolder;

        // Folders the audio import settings are applied to: short sounds (decompressed on load) and music
        // (compressed in memory; empty - no music clips).
        public static string SoundsFolder => Get().SoundsFolder;
        public static string MusicFolder => Get().MusicFolder;

        // Scenes pinned in Project Tools.
        public static IReadOnlyList<string> FavoriteScenes => Get().FavoriteScenes;

        // Folders of the scenes that Project Tools lists apart from the working ones, at the bottom.
        public static IReadOnlyList<string> TestSceneFolders => Get().TestSceneFolders;

        // Runtime assemblies dropped from player builds (PlayerAssemblyFilter).
        public static IReadOnlyList<string> StrippedPlayerAssemblies => Get().StrippedPlayerAssemblies;

        public static string Content(string relativePath)
        {
            return $"{ContentFolder}/{relativePath}";
        }

        public static string Package(string relativePath)
        {
            return $"{CorePackage}/{relativePath}";
        }

        private static Data Get()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var path = Path.Combine(projectRoot, SettingsPath);

            if (File.Exists(path) == false)
                throw new InvalidOperationException(
                    $"[FrameworkSettings] {SettingsPath} is missing. It describes the layout of the project for the " +
                    "framework editor tools: copy it from another project and adjust the folders.");

            // The file is edited by hand and pulled from git: a cached layout would keep writing into old folders.
            var info = new FileInfo(path);

            if (_data != null && info.LastWriteTimeUtc == _dataWriteTime && info.Length == _dataLength)
                return _data;

            var data = new Data();
            JsonConvert.PopulateObject(File.ReadAllText(path), data, new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            });

            Require(data.ContentAssembly, nameof(Data.ContentAssembly));

            // The sections of the content directory (0.7, removed in 0.8): the roots are those of the ContentBundle assets now, a
            // file that still lists them would build nothing, so it throws.
            if (data.Content != null || data.ExtraContent != null)
                throw new InvalidOperationException(
                    $"[FrameworkSettings] {SettingsPath} has \"Content\" / \"ExtraContent\": the content is ContentBundle assets now " +
                    "(Create > Framework > Content Bundle, its roots and IsPreloaded); remove the sections.");
            Require(data.ContentFolder, nameof(Data.ContentFolder));

            _data = data;
            _dataWriteTime = info.LastWriteTimeUtc;
            _dataLength = info.Length;
            return _data;
        }

        private static void Require(string value, string field)
        {
            if (string.IsNullOrEmpty(value))
                throw new InvalidOperationException($"[FrameworkSettings] {SettingsPath} has no {field}.");
        }

        private sealed class Data
        {
            public string ContentAssembly;
            public string ContentFolder;
            public List<string> ModuleRoots = new();
            public List<string> SceneFolders = new();
            public string ArtFolder = string.Empty;
            public string SoundsFolder = string.Empty;
            public string MusicFolder = string.Empty;
            public List<string> FavoriteScenes = new();
            public List<string> TestSceneFolders = new();
            public List<string> StrippedPlayerAssemblies = new();
            public object Content;
            public object ExtraContent;
        }
    }
}
