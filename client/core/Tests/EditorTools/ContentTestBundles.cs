using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal.EditorTests
{
    // ContentBundle assets of the tests, made in memory (an asset in the project would be a bundle of every player build)
    // and the target of their builds: the active build target, whose bundles the editor loads. Another target would cost
    // two things the tests must not do: ContentPipeline.BuildAssetBundles switches the active target to the built one
    // (SwitchToBuildPlatform, a recompilation of the scripts, left switched), and before that asks the build window of
    // the target whether it can build a player (CanBuildPlayer): the Linux window refuses Standalone on IL2CPP without the
    // toolchain package of the host, although bundles do not depend on the scripting backend.
    public static class ContentTestBundles
    {
        public static BuildTarget Target => EditorUserBuildSettings.activeBuildTarget;

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
