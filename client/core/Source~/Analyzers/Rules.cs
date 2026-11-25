using Microsoft.CodeAnalysis;

namespace Framework.Analyzers
{
    // The rules of CODE_STYLE_FULL.md and ARCHITECTURE_PRINCIPLES.md (P6, P11) of the framework docs. Every rule is a
    // warning by default; a game tunes it with dotnet_diagnostic.FWR00N.severity in .editorconfig or a ruleset.
    public static class Rules
    {
        private const string Category = "Framework";

        public static readonly DiagnosticDescriptor PublicSetter = new(
            "FWR001",
            "Public setter or public mutable field",
            "{0} '{1}' is changed from outside: make it {2}",
            Category,
            DiagnosticSeverity.Warning,
            true,
            "Data changes only through methods (CODE_STYLE_FULL.md). Allowed: [MemoryPackable] types, [Serializable] types " +
            "without logic read by JsonUtility, Blazor parameters, attribute arguments, overrides and implementations of a " +
            "foreign member.");

        public static readonly DiagnosticDescriptor FileLength = new(
            "FWR002",
            "File above the line limit",
            "The file has {0} lines, the limit is {1}: split it or say in the audit why it stays",
            Category,
            DiagnosticSeverity.Warning,
            true,
            "A file of runtime code or of an editor tool above 300 lines is split (CODE_STYLE_FULL.md, P11). Generated code, " +
            "tests and benchmarks have no limit.");

        public static readonly DiagnosticDescriptor TopLevelTypes = new(
            "FWR003",
            "More than one top-level type in a file",
            "'{0}' is a second top-level type in a file of '{1}': move it to its own file",
            Category,
            DiagnosticSeverity.Warning,
            true,
            "One top-level type per file (CODE_STYLE_FULL.md, P11). Allowed together: IX + X, X + XExtensions.");

        public static readonly DiagnosticDescriptor CommentLanguage = new(
            "FWR004",
            "Cyrillic in a comment",
            "The comment has Cyrillic text: comments are in English",
            Category,
            DiagnosticSeverity.Warning,
            true,
            "A comment is in English (CODE_STYLE_FULL.md).");

        public static readonly DiagnosticDescriptor LifetimeLock = new(
            "FWR005",
            "lock on a lifetime",
            "lock on '{0}', a lifetime: Lifetime locks on itself, take a private lock object",
            Category,
            DiagnosticSeverity.Warning,
            true,
            "Lifetime takes its own monitor; a lock on a lifetime outside Lifetime shares it and may deadlock with its listeners.");

        public static readonly DiagnosticDescriptor FileName = new(
            "FWR006",
            "File not named after its type",
            "The file '{0}' holds '{1}': name the file after its type",
            Category,
            DiagnosticSeverity.Warning,
            true,
            "The file is named after its top-level type (CODE_STYLE_FULL.md, P11); a part of a partial type may add a suffix " +
            "after a dot (X.Host.cs).");
    }
}
