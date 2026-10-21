using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Internal
{
    // The layout of the project the framework editor tools work in: where generated code and catalogue data go, which
    // folders hold game modules and scenes. Read from ProjectSettings/Framework.json; a missing file or field takes the
    // default, the layout of our projects.
    public static class FrameworkSettings
    {
        public const string SettingsPath = "ProjectSettings/Framework.json";
        public const string CorePackage = "Packages/com.noncasted.framework.core";

        private static Data _data;

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

        // Shared_* addressable groups: assets under these prefixes go to Shared_Common.
        public static IReadOnlyList<string> SharedCommonPaths => Get().SharedCommonPaths;

        // Addressable group -> the only phase that loads it (its shared dependencies go to Shared_<phase>).
        public static IReadOnlyDictionary<string, string> GroupPhases => Get().GroupPhases;

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
            if (_data != null)
                return _data;

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var path = Path.Combine(projectRoot, SettingsPath);
            var data = new Data();

            if (File.Exists(path) == true)
                JsonConvert.PopulateObject(File.ReadAllText(path), data, new JsonSerializerSettings
                {
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                });

            _data = data;
            return _data;
        }

        private sealed class Data
        {
            public string ContentAssembly = "Internal.Content";
            public string ContentFolder = "Assets/Common/Internal";
            public List<string> ModuleRoots = new() { "Assets/Common/Global", "Assets/GamePlay", "Assets/Menu" };
            public List<string> SceneFolders = new() { "Assets/Common", "Assets/GamePlay", "Assets/Menu", "Assets/Meta" };
            public string ArtFolder = "Assets/Art";
            public string SoundsFolder = "Assets/Art/Audio/Sounds";
            public string MusicFolder = "Assets/Art/Audio/Music";
            public List<string> FavoriteScenes = new() { "Startup", "Menu" };
            public List<string> SharedCommonPaths = new() { "Packages/", "Assets/Plugins/" };
            public Dictionary<string, string> GroupPhases = new(StringComparer.Ordinal);
            public List<string> StrippedPlayerAssemblies = new();
        }
    }
}
