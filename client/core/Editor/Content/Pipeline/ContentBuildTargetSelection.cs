using System;
using UnityEditor;

namespace Internal
{
    // Selects the target of a bundle build in the Build Settings for the time of the build. ContentPipeline.BuildAssetBundles
    // refuses to build ("Unable to build with the current configuration") when the build window of the target says it cannot
    // build a player, and that window checks the scripting backend of the selected target, not of the built one: a WebGL
    // project (IL2CPP) building the bundles of the editor platform (the tests) fails on the IL2CPP toolchain of Linux it never
    // uses. The selection only marks a target in the window, it does not switch the platform; the previous one comes back.
    public readonly struct ContentBuildTargetSelection : IDisposable
    {
        private readonly BuildTargetGroup _group;
        private readonly BuildTarget _standalone;

        private ContentBuildTargetSelection(BuildTargetGroup group, BuildTarget standalone)
        {
            _group = group;
            _standalone = standalone;
        }

        public static ContentBuildTargetSelection Of(BuildTarget target, BuildTargetGroup group)
        {
            var previous = new ContentBuildTargetSelection(EditorUserBuildSettings.selectedBuildTargetGroup, EditorUserBuildSettings.selectedStandaloneTarget);

            EditorUserBuildSettings.selectedBuildTargetGroup = group;

            if (group == BuildTargetGroup.Standalone)
                EditorUserBuildSettings.selectedStandaloneTarget = target;

            return previous;
        }

        public void Dispose()
        {
            EditorUserBuildSettings.selectedBuildTargetGroup = _group;
            EditorUserBuildSettings.selectedStandaloneTarget = _standalone;
        }
    }
}
