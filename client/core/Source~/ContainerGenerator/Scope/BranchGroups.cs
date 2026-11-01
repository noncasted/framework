using System.Collections.Generic;

namespace ContainerGenerator {
    // A group is what one if / switch chooses between: singletons that share a service and exclude each other. They
    // get one field. Two separate ifs do not exclude each other: both may run, each has its own field. A transient
    // has no field and is never grouped, it is chosen by its own IsRegistered flag.
    internal static class BranchGroups {
        public static List<List<int>> Build(List<GraphRegistration> registrations) {
            var groups = new List<List<int>>();
            for (var i = 0; i < registrations.Count; i++) {
                var registration = registrations[i];
                if (IsGrouped(registration) == false)
                    continue;

                var group = FindGroup(groups, registrations, registration);
                if (group == null) {
                    group = new List<int>();
                    groups.Add(group);
                }

                group.Add(i);
            }

            return groups;
        }

        public static bool IsGrouped(GraphRegistration registration) {
            return registration.Origin == "Alternative" && registration.Lifetime != "Transient";
        }

        // Different arms of one statement: at most one of them runs.
        public static bool Exclude(GraphRegistration left, GraphRegistration right) {
            if (string.IsNullOrEmpty(left.BranchPath) || string.IsNullOrEmpty(right.BranchPath))
                return false;

            var leftLevels = left.BranchPath.Split('/');
            var rightLevels = right.BranchPath.Split('/');
            for (var i = 0; i < leftLevels.Length && i < rightLevels.Length; i++) {
                if (leftLevels[i] == rightLevels[i])
                    continue;
                return Statement(leftLevels[i]) == Statement(rightLevels[i]);
            }

            return false;
        }

        private static string Statement(string level) {
            var dot = level.IndexOf('.');
            return dot < 0 ? level : level.Substring(0, dot);
        }

        private static List<int>? FindGroup(
            List<List<int>> groups,
            List<GraphRegistration> registrations,
            GraphRegistration candidate) {
            for (var g = 0; g < groups.Count; g++) {
                var shares = false;
                var excludes = true;
                for (var m = 0; m < groups[g].Count; m++) {
                    var member = registrations[groups[g][m]];
                    shares |= SharesService(member, candidate);
                    excludes &= Exclude(member, candidate);
                }

                if (shares && excludes)
                    return groups[g];
            }

            return null;
        }

        private static bool SharesService(GraphRegistration left, GraphRegistration right) {
            for (var i = 0; i < left.ServiceTypes.Count; i++) {
                if (right.ServiceTypes.Contains(left.ServiceTypes[i]))
                    return true;
            }

            return false;
        }
    }
}
