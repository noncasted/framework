using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class WalkLocal {
        private ITypeSymbol? _type;
        private bool _isBuilder;

        public string Name { get; init; } = "";
        public ITypeSymbol? Type { get => _type; init => _type = value; }
        public bool IsBuilder { get => _isBuilder; init => _isBuilder = value; }
        public bool FromInstantiate { get; private set; }
        public bool IsSwitch { get; private set; }
        public GraphRegistration? Registration { get; private set; }
        public int ReturnedCallIndex { get; private set; } = -1;

        public void SetType(ITypeSymbol? type) {
            _type = type;
        }

        public void MarkInstantiated() {
            FromInstantiate = true;
            _isBuilder = false;
        }

        public void MarkBuilder() {
            _isBuilder = true;
        }

        public void SetRegistration(GraphRegistration? registration) {
            Registration = registration;
        }

        public void SetReturnedCall(int index) {
            ReturnedCallIndex = index;
        }

        public void BindSwitch(GraphRegistration registration) {
            IsSwitch = true;
            Registration = registration;
        }
    }
}
