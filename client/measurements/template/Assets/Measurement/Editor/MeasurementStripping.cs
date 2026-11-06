using System;
using System.Collections.Generic;
using System.IO;
using Unity.Web.Stripping.Editor;
using UnityEditor;
using UnityEngine;

namespace Measurement
{
    /// <summary>
    ///     The submodule stripping of the Web Stripping Tool with the list of the variant, made active for the
    ///     build: the same settings pixel-swarm keeps in DefaultSubmoduleStrippingSettings.asset. A call of a stripped
    ///     submodule is ignored, as in pixel-swarm, or logged (<c>-measurementMissing LogError</c>) to find what a
    ///     broken variant calls; the logging adds code, so such a build is not a measurement.
    /// </summary>
    public static class MeasurementStripping
    {
        private const string Path = MeasurementAssets.Folder + "/SubmoduleStrippingSettings.asset";

        public static int Activate(IReadOnlyList<string> submodules, MissingSubmoduleErrorHandlingType missing)
        {
            if (submodules.Count == 0)
                throw new ArgumentException("The stripping list of the variant is empty");

            if (File.Exists(Path))
                AssetDatabase.DeleteAsset(Path);

            var settings = ScriptableObject.CreateInstance<SubmoduleStrippingSettings>();
            settings.OptimizeCodeAfterStripping = true;
            settings.RemoveEmbeddedDebugSymbols = true;
            settings.MissingSubmoduleErrorHandling = missing;
            settings.SubmodulesToStrip = new List<string>(submodules);

            AssetDatabase.CreateAsset(settings, Path);
            AssetDatabase.SaveAssets();

            StrippingProjectSettings.ActiveSettings = MeasurementAssets.Load<SubmoduleStrippingSettings>(Path);
            StrippingProjectSettings.StripAutomaticallyAfterBuild = true;

            return settings.SubmodulesToStrip.Count;
        }

        /// <summary>A build for the submodule profiling: the tool instruments only a build it has not stripped.</summary>
        public static int Deactivate()
        {
            StrippingProjectSettings.StripAutomaticallyAfterBuild = false;
            return 0;
        }
    }
}
