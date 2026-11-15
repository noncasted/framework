using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace Internal
{
    // Gives the linker of the player build the link.xml of the prepared bundles (ContentLinkXml); a build without them
    // adds nothing.
    public sealed class ContentLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            return ContentBuild.Current?.LinkXml;
        }
    }
}
