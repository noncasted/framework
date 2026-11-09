using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Internal
{
    // Builds the content directory of the settings into <output>/<name> (Library/Content for a player build): the
    // IContentBuildSteps first, then BuildPipeline.BuildContentDirectory from the roots, and reads its manifest.
    public static class ContentDirectoryBuilder
    {
        // The record of the types the content build serialized (scripts, serialized classes, native classes), in the
        // build report directory of the build.
        public const string TypeUsageFile = "ScriptsOnlyCache.yaml";

        // reportDirectory: the build report directory of the content build (BuildHistory). A player build given it
        // (BuildPlayerContext.AddPreviousBuildReportDirectory) keeps the types of the directory, which no scene of the
        // player may use: without it the linker strips them and the engine drops their objects at load.
        public static ContentManifest Build(ContentDirectorySettings directory, string output, out string reportDirectory)
        {
            foreach (var step in FindSteps())
                step.BeforeContentBuild(directory);

            foreach (var root in directory.Roots)
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(root, AssetPathToGUIDOptions.OnlyExistingAssets)))
                    throw new BuildFailedException($"[ContentBuild] The root {root} of the content directory {directory.Name} is not an asset");
            }

            var folder = $"{output}/{directory.Name}";

            if (Directory.Exists(folder))
                Directory.Delete(folder, true);

            var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
            {
                name = directory.Name,
                outputPath = folder,
                rootAssetPaths = directory.Roots.ToArray(),
                options = BuildContentOptions.FailBuildWhenErrorsLogged
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"[ContentBuild] The content directory {directory.Name} failed to build: {report.summary.result}");

            if (BuildHistory.TryGetBuildReportDirectory(report.summary.buildSessionGuid, out reportDirectory) == false)
                throw new BuildFailedException($"[ContentBuild] No build report directory of the content build {report.summary.buildSessionGuid} in the build history");

            if (File.Exists(Path.Combine(reportDirectory, TypeUsageFile)) == false)
                throw new BuildFailedException($"[ContentBuild] No {TypeUsageFile} in {reportDirectory}: the player would strip the types of the content directory");

            Debug.Log($"[ContentBuild] The content directory {directory.Name} built into {folder} in {report.summary.totalTime.TotalSeconds:0.0} s");
            return ContentManifest.Read(folder);
        }

        internal static IReadOnlyList<IContentBuildStep> FindSteps()
        {
            return TypeCache.GetTypesDerivedFrom<IContentBuildStep>()
                .Where(type => type.IsAbstract == false && type.ContainsGenericParameters == false)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(type => (IContentBuildStep)Activator.CreateInstance(type))
                .ToArray();
        }
    }
}
