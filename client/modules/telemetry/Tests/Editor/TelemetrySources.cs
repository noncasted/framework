using System.IO;
using System.Linq;
using UnityEditor;

namespace Global.Telemetry.Tests
{
    // The sources of the module on the disk: the folder differs between client/project and a game.
    public static class TelemetrySources
    {
        public static string ModuleFolder
        {
            get
            {
                var guid = AssetDatabase.FindAssets("TelemetrySources t:MonoScript").Single();
                var script = Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid));

                // <module>/Tests/Editor/TelemetrySources.cs
                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(script), "..", ".."));
            }
        }
    }
}
