using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Framework.Analyzers
{
    // The top-level type declarations of a file, in namespaces or not; nested types are not top-level. The parts of one
    // partial type and the generic variants of one name (Foo, Foo<T>) count as one type.
    public static class TopLevelTypes
    {
        public static IReadOnlyList<MemberDeclarationSyntax> Of(SyntaxNode root)
        {
            var result = new List<MemberDeclarationSyntax>();
            Collect(root, result);
            return result;
        }

        public static string NameOf(MemberDeclarationSyntax declaration)
        {
            return declaration switch
            {
                BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
                _ => throw new System.InvalidOperationException($"{declaration.Kind()} is not a type declaration")
            };
        }

        public static Location LocationOf(MemberDeclarationSyntax declaration)
        {
            return declaration switch
            {
                BaseTypeDeclarationSyntax type => type.Identifier.GetLocation(),
                DelegateDeclarationSyntax @delegate => @delegate.Identifier.GetLocation(),
                _ => throw new System.InvalidOperationException($"{declaration.Kind()} is not a type declaration")
            };
        }

        // The file name up to its first dot: X.Host.cs is a part of X.
        public static string FileStem(string path)
        {
            var name = System.IO.Path.GetFileName(path);
            var dot = name.IndexOf('.');
            return dot < 0 ? name : name.Substring(0, dot);
        }

        private static void Collect(SyntaxNode node, List<MemberDeclarationSyntax> result)
        {
            var members = node switch
            {
                CompilationUnitSyntax unit => unit.Members,
                BaseNamespaceDeclarationSyntax @namespace => @namespace.Members,
                _ => default
            };

            foreach (var member in members)
            {
                switch (member)
                {
                    case BaseNamespaceDeclarationSyntax @namespace:
                        Collect(@namespace, result);
                        break;
                    case BaseTypeDeclarationSyntax:
                    case DelegateDeclarationSyntax:
                        result.Add(member);
                        break;
                }
            }
        }
    }
}
