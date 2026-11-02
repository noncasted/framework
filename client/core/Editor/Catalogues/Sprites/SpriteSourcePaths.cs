using System;
using System.IO;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    [NoAutoStaticsCleanup]
    internal static class SpriteSourcePaths
    {
        private static readonly string[] SourceExtensions =
        {
            ".aseprite",
            ".ase",
            ".psd",
            ".png"
        };
        internal static bool IsSourceFile(string path)
        {
            var extension = Path.GetExtension(path);

            foreach (var sourceExtension in SourceExtensions)
            {
                if (extension.Equals(sourceExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static bool IsSourcePath(string path)
        {
            return string.IsNullOrEmpty(path) == false &&
                   path.StartsWith(FrameworkSettings.ArtFolder + "/", StringComparison.OrdinalIgnoreCase) &&
                   IsSourceFile(path);
        }
    }
}
