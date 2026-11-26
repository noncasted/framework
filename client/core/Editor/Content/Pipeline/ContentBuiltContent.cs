using System.Collections.Generic;

namespace Internal
{
    // What a content build wrote: its bundles and the serialized classes of their data, which the linker of the player build
    // must keep (ContentLinkXml): the class of a [SerializeReference] may be named by no code of the player.
    public sealed class ContentBuiltContent
    {
        public ContentBuiltContent(IReadOnlyList<ContentBuiltBundle> bundles, IReadOnlyList<ContentSerializedType> serializedTypes)
        {
            Bundles = bundles;
            SerializedTypes = serializedTypes;
        }

        public IReadOnlyList<ContentBuiltBundle> Bundles { get; }
        public IReadOnlyList<ContentSerializedType> SerializedTypes { get; }
    }
}
