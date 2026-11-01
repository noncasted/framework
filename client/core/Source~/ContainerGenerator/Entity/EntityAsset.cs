using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class EntityAsset {
        public string AssetPath = "";
        public string HolderType = "";
        public List<string> ComponentTypes = new List<string>();
    }
}
