using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Framework.Analyzers
{
    // Third-party code gets no rule at all. Unity passes no .editorconfig to its compiler, so the default is decided by
    // the path, the same in Unity and in dotnet build: a folder PackageCache (registry packages in Library/PackageCache),
    // a package folder (its name has a dot, com.x.y) inside a folder named Packages / packages (the embedded packages of
    // a game in client/Packages, the forks of the framework in client/packages), or the folder Plugins right under
    // Assets. The option framework_rules.third_party (true / false) of .editorconfig wins over the path where an
    // .editorconfig is read (dotnet build).
    public static class ThirdParty
    {
        public const string Option = "framework_rules.third_party";

        public static bool Is(SyntaxTree tree, AnalyzerOptions options)
        {
            var config = options.AnalyzerConfigOptionsProvider.GetOptions(tree);

            if (config.TryGetValue(Option, out var value) == false)
                return IsThirdPartyPath(tree.FilePath);

            switch (value)
            {
                case "true":
                    return true;
                case "false":
                    return false;
                default:
                    throw new InvalidOperationException($"{Option} = '{value}' is neither true nor false");
            }
        }

        // A symbol of the sources is third-party when its first declaration is.
        public static bool Is(ISymbol symbol, AnalyzerOptions options)
        {
            var tree = symbol.Locations[0].SourceTree
                ?? throw new InvalidOperationException($"'{symbol}' has no source declaration");

            return Is(tree, options);
        }

        private static bool IsThirdPartyPath(string path)
        {
            var segments = path.Replace('\\', '/').Split('/');

            // The last segment is the file itself.
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var segment = segments[i].ToLowerInvariant();

                if (segment == "packagecache")
                    return true;

                if (segment == "packages" && i + 1 < segments.Length - 1 && segments[i + 1].IndexOf('.') > 0)
                    return true;

                if (segment == "plugins" && i > 0 && segments[i - 1].ToLowerInvariant() == "assets")
                    return true;
            }

            return false;
        }
    }
}
