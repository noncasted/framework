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
                Ordinal = state.TakeOrdinal(),
                Location = LocationInfo.CreateFrom(node.GetLocation()),
                BranchPath = string.Join("/", state.Branches),
            };
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
    }
}
