using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class EntityAsset {
        public string AssetPath { get; init; } = "";
        public string HolderType { get; init; } = "";
        public List<string> ComponentTypes { get; } = new List<string>();
    }
}
