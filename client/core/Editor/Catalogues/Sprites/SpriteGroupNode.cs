using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class SpriteGroupNode
    {
        public SpriteGroupNode(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }
        public SpriteGroupDefinition Group { get; private set; }
        public SortedDictionary<string, SpriteGroupNode> Children { get; } = new(StringComparer.Ordinal);

        public void SetGroup(SpriteGroupDefinition group)
        {
            Group = group;
        }

        public string ClassName => Group != null ? Group.ClassName : CatalogNaming.ToGroupKey(Path) + "Sprites";
    }
}
