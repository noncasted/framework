using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal.EditorTests
{
    // ContentBundle assets of the tests, made in memory (an asset in the project would be a bundle of every player build)
    // and the target of a build the editor loads.
    public static class ContentTestBundles
    {
        public static BuildTarget EditorTarget => Application.platform switch
        {
            RuntimePlatform.WindowsEditor => BuildTarget.StandaloneWindows64,
            RuntimePlatform.OSXEditor => BuildTarget.StandaloneOSX,
            _ => BuildTarget.StandaloneLinux64
        };

        public static ContentBundle Create(string name, params Object[] roots)
        {
            return Create(name, false, false, roots);
        }

        public static ContentBundle Create(string name, bool isPreloaded, bool isGzip, IReadOnlyList<Object> roots)
        {
            var bundle = ScriptableObject.CreateInstance<ContentBundle>();
            bundle.name = name;
            bundle.Init(roots, isPreloaded, isGzip);
            return bundle;
        }
    }
}
