using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal static class RegistrationFactory {
        public static GraphRegistration NewRegistration(
            WalkState state,
            SyntaxNode node,
            string kind,
            string implementation,
            string origin) {
            var span = node.GetLocation().GetLineSpan();
            if (state.Branches.Count > 0 && origin == "ConstructedType")
                origin = "Alternative";

            return new GraphRegistration {
                Kind = kind,
                ImplementationType = implementation,
                Lifetime = "Singleton",
                Origin = origin,
                Source = WalkSyntax.Trim(node.ToString()),
                File = node.SyntaxTree.FilePath ?? "",
                Line = span.StartLinePosition.Line + 1,
                Ordinal = state.NextOrdinal++,
                Location = LocationInfo.CreateFrom(node.GetLocation()),
                BranchPath = string.Join("/", state.Branches),
            };
        }

        // A local names its registration or its installer call; LastRegistration is only for a chain without one.
        public static GraphRegistration? CurrentRegistration(WalkState state, InvocationExpressionSyntax invocation) {
            var receiver = WalkSyntax.GetReceiver(invocation);
            if (receiver != null &&
                WalkSyntax.Unwrap(receiver) is IdentifierNameSyntax identifier &&
                state.Locals.TryGetValue(identifier.Identifier.Text, out var local)) {
                if (local.Registration != null) {
                    state.LastRegistration = local.Registration;
                    state.LastCallIndex = -1;
                    return local.Registration;
                }

                if (local.ReturnedCallIndex >= 0) {
                    state.LastRegistration = null;
                    state.LastCallIndex = local.ReturnedCallIndex;
                    return null;
                }
            }

            return state.LastRegistration;
        }

        public static string ClassifyValue(WalkState state, ExpressionSyntax? expression) {
            if (expression == null)
                return "InstanceHole";

            expression = WalkSyntax.Unwrap(expression);
            if (expression is IdentifierNameSyntax identifier && state.Locals.TryGetValue(identifier.Identifier.Text, out var local)) {
                if (local.FromInstantiate)
                    return "PrefabInstance";
            }

            if (LooksLikePrefabAsset(expression))
                return "PrefabAsset";

            return "InstanceHole";
        }

        public static bool LooksLikePrefabAsset(ExpressionSyntax expression) {
            var text = expression.ToString();
            return text.IndexOf("Prefabs.", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("Prefab", StringComparison.Ordinal) >= 0 && text.IndexOf("Instantiate", StringComparison.Ordinal) < 0;
        }

        public static void AddService(GraphRegistration registration, string service, string map = "") {
            if (string.IsNullOrEmpty(service))
                return;
            if (registration.ServiceTypes.Contains(service))
                return;
            registration.ServiceTypes.Add(service);
            while (registration.ServiceMaps.Count < registration.ServiceTypes.Count - 1)
                registration.ServiceMaps.Add("");
            registration.ServiceMaps.Add(map ?? "");
        }

        public static string CombineHole(string existing, string next) {
            if (string.IsNullOrEmpty(existing))
                return next;
            if (string.IsNullOrEmpty(next))
                return existing;
            return existing + "; " + next;
        }

        public static string Format(WalkState state, ITypeSymbol? type) {
            if (type == null)
                return "";
            return TypeSubstitution.Template(state.Method, type);
        }

        public static string Map(WalkState state, ITypeSymbol? type) {
            if (type == null)
                return "";
            return TypeSubstitution.Map(state.Method, type);
        }
    }
}
