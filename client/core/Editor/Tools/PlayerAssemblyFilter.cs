using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    /// <summary>
    /// Drops runtime assemblies of packages that only editor code needs.
    /// - MCPForUnity.Runtime: the linker keeps it because of a MonoBehaviour inside, and its converters
    ///   drag Newtonsoft.Json into the player.
    /// - FrameworkSettings.StrippedPlayerAssemblies: the project's own list, e.g. Unity.2D.Tilemap.Runtime (comes
    ///   with the Aseprite importer, is [AlwaysLinkAssembly] and keeps the Tilemap and Physics2D modules) in a game
    ///   without tilemaps.
    /// </summary>
    public class PlayerAssemblyFilter : IFilterBuildAssemblies
    {
        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            var names = new HashSet<string>(FrameworkSettings.StrippedPlayerAssemblies) { "MCPForUnity.Runtime" };

            return assemblies
                   .Where(path => names.Contains(Path.GetFileNameWithoutExtension(path)) == false)
                   .ToArray();
        }
    }
}
