using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class WalkLocal {
        public string Name = "";
        public ITypeSymbol? Type;
        public bool IsBuilder;
        public bool FromInstantiate;
        public bool IsSwitch;
        public GraphRegistration? Registration;
        public int ReturnedCallIndex = -1;
    }
}
