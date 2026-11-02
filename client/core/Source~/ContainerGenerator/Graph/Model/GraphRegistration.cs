using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphRegistration {
        public string Kind = "Register";
        public string ImplementationType = "";
        public List<string> ServiceTypes = new List<string>();
        public string Lifetime = "Singleton";
        public string Origin = "ConstructedType";
        public string Source = "";
        public string Hole = "";
        public string File = "";
        public int Line;
        public int Ordinal;
        public string TypeMap = "";
        public List<string> ServiceMaps = new List<string>();
        public LocationInfo? Location;
        public List<GraphSwitchArm> Arms = new List<GraphSwitchArm>();
        public List<GraphEdge> Dependencies = new List<GraphEdge>();
        // The name of the [Inject] method; RegistrationBinder sets it together with the edges, so Clone does not copy it.
        public string InjectMethod = "";
        // Where the registration stands among the if / switch statements of its method: "statement.arm" per level.
        // Two registrations exclude each other when they took different arms of one statement (BranchGroups).
        public string BranchPath = "";
        // The types given by WithParameter: a constructor / [Inject] parameter of such a type takes that value first.
        public List<string> ParameterTypes = new List<string>();

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

        public void AddHole(string hole) {
            Hole = CombineHole(Hole, hole);
            if (Arms.Count == 0 && Origin == "ConstructedType")
                Origin = "ParameterHole";
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
