using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator.Verify {
    // A root of a scope is a method group or a local function: the runtime finds the generated class by the name of the
    // method (GeneratedScopes.RootId), a lambda has none and fails every load of its scope.
    internal static partial class Program {
        private static void RunLambdaRootCases() {
            Run("CINGR017 lambda given as the root of a scope", () => RequireLambdaRootError(LambdaRoot, "LoadRoot"));
            Run("CINGR017 lambda given to the constructor of the load options", () => RequireLambdaRootError(LambdaOptionsRoot, "ScopeLoadOptions"));
            Run("CINGR017 anonymous method given as the root of a scope", () => RequireLambdaRootError(AnonymousMethodRoot, "LoadRoot"));
            Run("CINGR017 lambda kept in a variable of the root type", () => RequireLambdaRootError(LambdaVariableRoot, "Func<IScopeBuilder, UniTask>"));
            Run("CINGR017 lambda given as the root of an entity", () => RequireLambdaRootError(LambdaEntityRoot, "Load"));
            Run("a local function that closes over a value is a root", TestClosureLocalRoot);
            Run("a lambda of an inline installer is not a root", TestInlineInstallerLambda);
        }

        private static void RequireLambdaRootError(string source, string target) {
            var run = GenerateWithRuntime(source);
            var errors = run.Diagnostics.Where(d => d.Id == "CINGR017").ToArray();
            Require(errors.Length == 1, "one CINGR017 expected:\n" + AllErrors(run));
            var error = errors[0];
            Require(error.Severity == DiagnosticSeverity.Error, "CINGR017 severity " + error.Severity);
            Require(error.Location != Location.None, "CINGR017 must point at the lambda");
            var message = error.GetMessage();
            Require(message.IndexOf("'" + target + "'", StringComparison.Ordinal) >= 0, "CINGR017 must name " + target + ": " + message);
            Require(message.IndexOf("local function", StringComparison.Ordinal) >= 0, "CINGR017 must say what to do: " + message);
            var others = run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CINGR017").ToArray();
            Require(others.Length == 0, "only CINGR017 expected:\n" + string.Join("\n", others.Select(d => d.ToString())));
            RequireGeneratedContains(run.Output, "class HealthyRootConstructContainer");
        }

        private static void TestClosureLocalRoot() {
            var run = GenerateWithRuntime(ClosureLocalRoot);
            RequireNoGeneratorErrors(run);
            RequireGeneratedContains(run.Output, "Sample.Loads.LoadInternal+Construct");
        }

        private static void TestInlineInstallerLambda() {
            var run = GenerateWithRuntime(InlineInstallerLambda);
            RequireNoGeneratorErrors(run);
        }

        // The entries of a scope root as the core has them (Runtime/Scopes), reduced to their signatures.
        private const string RootEntries = @"
namespace Cysharp.Threading.Tasks {
    public struct UniTask { public static UniTask CompletedTask => default; }
}

namespace Internal {
    using Cysharp.Threading.Tasks;
    public class ScopeLoadOptions {
        public ScopeLoadOptions(object parent, Func<IScopeBuilder, UniTask> constructCallback) {}
    }
    public static class RootScopeExtensions {
        public static void LoadRoot(this object loader, Func<IScopeBuilder, UniTask> construct) {}
    }
}

namespace Sample {
    using Cysharp.Threading.Tasks;
    public class Service {}
    public class Options {}
    public class View : IScopeEntityView {}
    public static class HealthyRoot {
        public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
    }
    public static class Installers {
        public static UniTask Install(IScopeBuilder builder, Options options) { builder.Register<Service>(); return UniTask.CompletedTask; }
        public static void InstallEntity(IEntityBuilder builder, Options options) { builder.Register<Service>(); }
    }
}
";

        private const string LambdaRoot = Usings + RootEntries + @"
namespace Sample {
    using Cysharp.Threading.Tasks;
    public static class Loads {
        public static void LoadInternal(object loader, Options options) {
            loader.LoadRoot(builder => Installers.Install(builder, options));
        }
    }
}";

        private const string LambdaOptionsRoot = Usings + RootEntries + @"
namespace Sample {
    using Cysharp.Threading.Tasks;
    public static class Loads {
        public static ScopeLoadOptions LoadInternal(Options options) {
            return new ScopeLoadOptions(null, builder => Installers.Install(builder, options));
        }
    }
}";

        private const string AnonymousMethodRoot = Usings + RootEntries + @"
namespace Sample {
    using Cysharp.Threading.Tasks;
    public static class Loads {
        public static void LoadInternal(object loader, Options options) {
            loader.LoadRoot(delegate(IScopeBuilder builder) { return Installers.Install(builder, options); });
        }
    }
}";

        private const string LambdaVariableRoot = Usings + RootEntries + @"
namespace Sample {
    using Cysharp.Threading.Tasks;
    public static class Loads {
        public static void LoadInternal(object loader, Options options) {
            Func<IScopeBuilder, UniTask> construct = builder => Installers.Install(builder, options);
            loader.LoadRoot(construct);
        }
    }
}";

        private const string LambdaEntityRoot = Usings + RootEntries + @"
namespace Sample {
    public class Loads {
        private readonly IEntityScopeLoader _loader;
        private readonly View _view;
        public Loads(IEntityScopeLoader loader, View view) { _loader = loader; _view = view; }
        public void Load(Options options) { _loader.Load(null, null, _view, builder => Installers.InstallEntity(builder, options)); }
    }
}";

        private const string ClosureLocalRoot = Usings + RootEntries + @"
namespace Sample {
    using Cysharp.Threading.Tasks;
    public static class Loads {
        public static void LoadInternal(object loader, Options options) {
            loader.LoadRoot(Construct);

            UniTask Construct(IScopeBuilder builder) { return Installers.Install(builder, options); }
        }
    }
}";

        private const string InlineInstallerLambda = Usings + RootEntries + @"
namespace Sample {
    public static class Inline {
        public static void Each(this IScopeBuilder builder, Action<IScopeBuilder> install) { install(builder); }
    }
    public static class InlineRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Each(inner => inner.Register<Service>());
        }
    }
}";
    }
}
