using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphRegistration {
        private string _implementationType = "";
        private string _lifetime = "Singleton";
        private string _origin = "ConstructedType";
        private string _hole = "";
        private string _typeMap = "";
        private string _branchPath = "";

        public string Kind { get; init; } = "Register";
        public string ImplementationType { get => _implementationType; init => _implementationType = value; }
        public List<string> ServiceTypes { get; } = new List<string>();
        public string Lifetime { get => _lifetime; init => _lifetime = value; }
        public string Origin { get => _origin; init => _origin = value; }
        public string Source { get; init; } = "";
        public string Hole { get => _hole; init => _hole = value; }
        public string File { get; init; } = "";
        public int Line { get; init; }
        public int Ordinal { get; init; }
        public string TypeMap { get => _typeMap; init => _typeMap = value; }
        public List<string> ServiceMaps { get; } = new List<string>();
        public LocationInfo? Location { get; init; }
        public List<GraphSwitchArm> Arms { get; } = new List<GraphSwitchArm>();
        public List<GraphEdge> Dependencies { get; } = new List<GraphEdge>();
        // The name of the [Inject] method; RegistrationBinder sets it together with the edges, so Clone does not copy it.
        public string InjectMethod { get; private set; } = "";
        // Where the registration stands among the if / switch statements of its method: "statement.arm" per level.
        // Two registrations exclude each other when they took different arms of one statement (BranchGroups).
        public string BranchPath { get => _branchPath; init => _branchPath = value; }
        // The types given by WithParameter: a constructor / [Inject] parameter of such a type takes that value first.
        public List<string> ParameterTypes { get; } = new List<string>();

        public GraphRegistration Clone() {
            var copy = new GraphRegistration {
                Kind = Kind,
                ImplementationType = ImplementationType,
                Lifetime = Lifetime,
                Origin = Origin,
                Source = Source,
                Hole = Hole,
                File = File,
                Line = Line,
                Ordinal = Ordinal,
                TypeMap = TypeMap,
                Location = Location,
                BranchPath = BranchPath,
            };
            copy.ParameterTypes.AddRange(ParameterTypes);
            copy.ServiceTypes.AddRange(ServiceTypes);
            copy.ServiceMaps.AddRange(ServiceMaps);
            for (var i = 0; i < Arms.Count; i++)
                copy.Arms.Add(Arms[i].Clone());
            return copy;
        }

        public void AddService(string service, string map = "") {
            if (string.IsNullOrEmpty(service))
                return;
            if (ServiceTypes.Contains(service))
                return;
            ServiceTypes.Add(service);
            while (ServiceMaps.Count < ServiceTypes.Count - 1)
                ServiceMaps.Add("");
            ServiceMaps.Add(map ?? "");
        }

        public void SetHole(string hole) {
            _hole = hole;
        }

        public void SetLifetime(string lifetime) {
            _lifetime = lifetime;
        }

        public void SetTypeMap(string typeMap) {
            _typeMap = typeMap;
        }

        public void BindInjectMethod(string method) {
            InjectMethod = method;
        }

        // The statements of a method are numbered inside it, a generic method is walked per call.
        public void PrefixBranchPath(int walk) {
            _branchPath = walk + "/" + BranchPath;
        }

        // A copy of an open generic registration with its type arguments known: the map of the placeholders is spent.
        public void Close(string implementationType) {
            _implementationType = implementationType;
            _typeMap = "";
        }

        public void AddHole(string hole) {
            _hole = CombineHole(Hole, hole);
            if (Arms.Count == 0 && Origin == "ConstructedType")
                _origin = "ParameterHole";
        }

        public string FirstService() {
            for (var i = 0; i < ServiceTypes.Count; i++) {
                if (string.IsNullOrEmpty(ServiceTypes[i]) == false)
                    return ServiceTypes[i];
            }

            return "";
        }

        public string InferDiscriminantType() {
            if (Arms.Count == 0)
                return "object";

            var pattern = Arms[0].Discriminant ?? "";
            var dot = pattern.LastIndexOf('.');
            if (dot <= 0)
                return "object";

            var type = pattern.Substring(0, dot).Trim();
            if (type.StartsWith("global::", StringComparison.Ordinal) == false && type.IndexOf('.') >= 0)
                type = "global::" + type;
            if (string.IsNullOrEmpty(type))
                return "object";
            return type;
        }

        private static string CombineHole(string existing, string next) {
            if (string.IsNullOrEmpty(existing))
                return next;
            if (string.IsNullOrEmpty(next))
                return existing;
            return existing + "; " + next;
        }
    }
}
