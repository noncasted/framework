using System;

namespace ContainerGenerator {
    internal sealed class ParameterModel : IEquatable<ParameterModel> {
        public string TypeFullName { get; }
        public string Name { get; }

        public ParameterModel(string typeFullName, string name) {
            TypeFullName = typeFullName;
            Name = name;
        }

        public bool Equals(ParameterModel? other) {
            return other != null && TypeFullName == other.TypeFullName && Name == other.Name;
        }

        public override bool Equals(object? obj) {
            return Equals(obj as ParameterModel);
        }

        public override int GetHashCode() {
            return HashCodes.Combine(HashCodes.Of(TypeFullName), HashCodes.Of(Name));
        }
    }
}
