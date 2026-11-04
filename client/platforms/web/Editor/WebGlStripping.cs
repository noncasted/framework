using System;
using Unity.Web.Stripping.Editor;

namespace Internal
{
    /// <summary>
    ///     Makes the submodule stripping of the Web Stripping Tool run after a WebGL build: the package skips it
    ///     silently when its settings are not loaded yet (batchmode) or the post-build flag is off.
    ///     The active settings asset belongs to the game (the project settings of the package).
    /// </summary>
    public static class WebGlStripping
    {
        public static void Require()
        {
            StrippingProjectSettings.LoadSettings();

            var settings = StrippingProjectSettings.ActiveSettings;

            if (settings == null)
            {
                throw new Exception(
                    "No active submodule stripping settings: set them in "
                    + "ProjectSettings/Packages/com.unity.web.stripping-tool/Settings.json "
                    + "(Assets > Web Optimization > Set as Active Submodule Stripping Settings).");
            }

            if (settings.SubmodulesToStrip.Count == 0)
                throw new Exception($"Submodule stripping settings {settings.name} list no submodules to strip.");

            if (StrippingProjectSettings.StripAutomaticallyAfterBuild == false)
                StrippingProjectSettings.StripAutomaticallyAfterBuild = true;
        }
    }
}
