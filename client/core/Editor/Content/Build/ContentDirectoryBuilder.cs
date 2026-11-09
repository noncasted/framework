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
        public static ContentManifest Build(ContentDirectorySettings directory, string output)
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
