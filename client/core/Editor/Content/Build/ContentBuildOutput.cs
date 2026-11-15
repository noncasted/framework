using System.Collections.Generic;

namespace Internal
{
    // The bundles prepared for the running player build: the folder the player gets as StreamingAssets/Content, the link.xml
    // of the types of their objects (ContentLinkerProcessor) and the catalog the player gets in Resources.
    public sealed class ContentBuildOutput
    {
        public ContentBuildOutput(string shipped, string linkXml, ContentCatalog catalog, IReadOnlyList<ContentBuiltBundle> bundles)
        {
            Shipped = shipped;
            LinkXml = linkXml;
            Catalog = catalog;
            Bundles = bundles;
        }

        public string Shipped { get; }
        public string LinkXml { get; }
        public ContentCatalog Catalog { get; }
        public IReadOnlyList<ContentBuiltBundle> Bundles { get; }
    }
}
