using System;

namespace ContainerGenerator {
    internal sealed class InjectorModel : IEquatable<InjectorModel> {
        public string TypeName { get; }
        public string FullTypeName { get; }
        public string Namespace { get; }
        public string SymbolName { get; }
        public bool IsNested { get; }
        public bool IsAbstract { get; }
        public bool IsGeneric { get; }
        public bool IsUnityObject { get; }
        public bool HasConstruct { get; }
        public bool ConstructAccessible { get; }
        public bool ConstructIsGeneric { get; }
        public bool MultipleConstruct { get; }
        public bool ConstructorFound { get; }
        public bool ConstructorAccessible { get; }
        public EquatableArray<ParameterModel> ConstructorParameters { get; }
        public EquatableArray<ParameterModel> ConstructParameters { get; }
        public string ConstructName { get; }
        public LocationInfo? Location { get; }

        public InjectorModel(
            string typeName,
            string fullTypeName,
            string ns,
            string symbolName,
            bool isNested,
            bool isAbstract,
            bool isGeneric,
            bool isUnityObject,
            bool hasConstruct,
            bool constructAccessible,
            bool constructIsGeneric,
            bool multipleConstruct,
            bool constructorFound,
            bool constructorAccessible,
            EquatableArray<ParameterModel> constructorParameters,
            EquatableArray<ParameterModel> constructParameters,
            string constructName,
            LocationInfo? location) {
            TypeName = typeName;
            FullTypeName = fullTypeName;
            Namespace = ns;
            SymbolName = symbolName;
            IsNested = isNested;
            IsAbstract = isAbstract;
            IsGeneric = isGeneric;
            IsUnityObject = isUnityObject;
            HasConstruct = hasConstruct;
            ConstructAccessible = constructAccessible;
            ConstructIsGeneric = constructIsGeneric;
            MultipleConstruct = multipleConstruct;
            ConstructorFound = constructorFound;
            ConstructorAccessible = constructorAccessible;
            ConstructorParameters = constructorParameters;
            ConstructParameters = constructParameters;
            ConstructName = constructName;
            Location = location;
        }

        public string HintName => TypeNames.SafeHint(FullTypeName) + "GeneratedInjector.g.cs";

        public string InjectorTypeName => TypeNames.SafeHint(FullTypeName) + "GeneratedInjector";

        public bool Equals(InjectorModel? other) {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return TypeName == other.TypeName &&
                   FullTypeName == other.FullTypeName &&
                   Namespace == other.Namespace &&
                   SymbolName == other.SymbolName &&
                   IsNested == other.IsNested &&
                   IsAbstract == other.IsAbstract &&
                   IsGeneric == other.IsGeneric &&
                   IsUnityObject == other.IsUnityObject &&
                   HasConstruct == other.HasConstruct &&
                   ConstructAccessible == other.ConstructAccessible &&
                   ConstructIsGeneric == other.ConstructIsGeneric &&
                   MultipleConstruct == other.MultipleConstruct &&
                   ConstructorFound == other.ConstructorFound &&
                   ConstructorAccessible == other.ConstructorAccessible &&
                   ConstructorParameters.Equals(other.ConstructorParameters) &&
                   ConstructParameters.Equals(other.ConstructParameters) &&
                   ConstructName == other.ConstructName;
        }

        public override bool Equals(object? obj) {
            return Equals(obj as InjectorModel);
        }

        public override int GetHashCode() {
            var hash = HashCodes.Of(FullTypeName);
            hash = HashCodes.Combine(hash, HashCodes.Of(IsNested));
            hash = HashCodes.Combine(hash, HashCodes.Of(IsGeneric));
            hash = HashCodes.Combine(hash, HashCodes.Of(HasConstruct));
            hash = HashCodes.Combine(hash, ConstructorParameters.GetHashCode());
            hash = HashCodes.Combine(hash, ConstructParameters.GetHashCode());
            hash = HashCodes.Combine(hash, HashCodes.Of(ConstructName));
            return hash;
        }
    }
}
