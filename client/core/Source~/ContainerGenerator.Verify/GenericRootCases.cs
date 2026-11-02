using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator.Verify {
    // A root of a container cannot be generic in any form; the name of a generated source does not limit the roots.
    internal static partial class Program {
        private static void RunGenericRootCases() {
            Run("CINGR016 generic method with the root attribute", () => RequireGenericRootError(GenericMethodRoot, "Sample.GenericRoot.Make<T>"));
            Run("CINGR016 generic method named Construct", () => RequireGenericRootError(GenericConstructRoot, "Sample.GenericRoot.Construct<T>"));
            Run("CINGR016 method of a generic type", () => RequireGenericRootError(GenericTypeRoot, "Sample.GenericRoot<T>.Construct"));
            Run("CINGR016 for each generic type of one name", TestGenericTypesOfOneName);
            Run("CINGR016 method of a type nested in a generic type", () => RequireGenericRootError(NestedInGenericRoot, "Sample.Outer<T>.Inner.Construct"));
            Run("CINGR016 local function of a generic method", () => RequireGenericRootError(LocalInGenericRoot, "Sample.GenericRoot.Load<T>+Construct"));
            Run("CINGR016 generic local function", () => RequireGenericRootError(GenericLocalRoot, "Sample.GenericRoot.Load+Construct"));
            Run("CINGR016 generic entity root", () => RequireGenericRootError(GenericEntityRoot, "Sample.GenericRoot.Make<T>"));
            Run("CINGR016 generic entity root with two views", () => RequireGenericRootError(GenericEntityVariantRoot, "Sample.GenericRoot.Make<T>"));
            Run("CINGR016 once for a generic Construct given to the entity loader", () => RequireGenericRootError(GenericEntityConstructRoot, "Sample.GenericRoot.Construct<T>"));
            Run("roots of one type name in two namespaces get their sources", TestRootsOfOneNameInTwoNamespaces);
        }

        private static void RequireGenericRootError(string source, string root) {
            var run = GenerateWithRuntime(source);
            var errors = run.Diagnostics.Where(d => d.Id == "CINGR016").ToArray();
            Require(errors.Length == 1, "one CINGR016 expected:\n" + AllErrors(run));
            RequireGenericRootMessage(errors[0], root);
            RequireOnlyGenericRootErrors(run);
        }

        private static void TestGenericTypesOfOneName() {
            var run = GenerateWithRuntime(GenericTypesOfOneNameRoot);
            var errors = run.Diagnostics.Where(d => d.Id == "CINGR016").ToArray();
            Require(errors.Length == 2, "two CINGR016 expected:\n" + AllErrors(run));
            RequireGenericRootMessage(errors.Single(e => e.GetMessage().Contains("GenericRoot<T>.")), "Sample.GenericRoot<T>.Construct");
            RequireGenericRootMessage(errors.Single(e => e.GetMessage().Contains("GenericRoot<T, V>.")), "Sample.GenericRoot<T, V>.Construct");
            RequireOnlyGenericRootErrors(run);
        }

        private static void RequireGenericRootMessage(Diagnostic error, string root) {
            Require(error.Severity == DiagnosticSeverity.Error, "CINGR016 severity " + error.Severity);
            Require(error.Location != Location.None, "CINGR016 must point at the root");
            var message = error.GetMessage();
            Require(message.IndexOf("'" + root + "'", StringComparison.Ordinal) >= 0, "CINGR016 must name the root " + root + ": " + message);
            Require(message.IndexOf("generic installer", StringComparison.Ordinal) >= 0, "CINGR016 must say what to do: " + message);
        }

        // Nothing but the clear error: the scopes that are fine still get their classes.
        private static void RequireOnlyGenericRootErrors(GeneratorRun run) {
            var others = run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CINGR016").ToArray();
            Require(others.Length == 0, "only CINGR016 expected:\n" + string.Join("\n", others.Select(d => d.ToString())));
            RequireGeneratedContains(run.Output, "class HealthyRootConstructContainer");
            RequireGeneratedMissing(run.Output, "class GenericRoot");
        }

        private static string AllErrors(GeneratorRun run) {
            return string.Join("\n", run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
        }

        private static void TestRootsOfOneNameInTwoNamespaces() {
            var run = GenerateWithRuntime(RootsInTwoNamespaces);
            RequireNoGeneratorErrors(run);
            RequireGeneratedContains(run.Output, "namespace First");
            RequireGeneratedContains(run.Output, "namespace Second");
        }

        private const string HealthyRoot = @"
    public class Service {}
    public class View : IScopeEntityView {}
    public class OtherView : IScopeEntityView {}
    public static class HealthyRoot {
        public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
    }";

        private const string GenericMethodRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public static class GenericRoot {
        [ContainerGraphRoot]
        public static void Make<T>(IScopeBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string GenericConstructRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public static class GenericRoot {
        public static void Construct<T>(IScopeBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string GenericTypeRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public static class GenericRoot<T> {
        public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string GenericTypesOfOneNameRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public static class GenericRoot<T> {
        public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
    }
    public static class GenericRoot<T, V> {
        public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string NestedInGenericRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public static class Outer<T> {
        public static class Inner {
            public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
        }
    }
}";

        private const string LocalInGenericRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public static class GenericRoot {
        public static void Load<T>() {
            void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
            Construct(null);
        }
    }
}";

        private const string GenericLocalRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public static class GenericRoot {
        public static void Load() {
            void Construct<T>(IScopeBuilder builder) { builder.Register<Service>(); }
            Construct<int>(null);
        }
    }
}";

        private const string GenericEntityRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public class GenericRoot {
        private readonly IEntityScopeLoader _loader;
        private readonly View _view;
        public GenericRoot(IEntityScopeLoader loader, View view) { _loader = loader; _view = view; }
        public void Load() { _loader.Load(null, null, _view, Make<int>); }
        private static void Make<T>(IEntityBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string GenericEntityVariantRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public class GenericRoot {
        private readonly IEntityScopeLoader _loader;
        private readonly View _view;
        private readonly OtherView _other;
        public GenericRoot(IEntityScopeLoader loader, View view, OtherView other) { _loader = loader; _view = view; _other = other; }
        public void Load() {
            _loader.Load(null, null, _view, Make<int>);
            _loader.Load(null, null, _other, Make<string>);
        }
        private static void Make<T>(IEntityBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string GenericEntityConstructRoot = Usings + "namespace Sample {" + HealthyRoot + @"
    public class GenericRoot {
        private readonly IEntityScopeLoader _loader;
        private readonly View _view;
        public GenericRoot(IEntityScopeLoader loader, View view) { _loader = loader; _view = view; }
        public void Load() { _loader.Load(null, null, _view, Construct<int>); }
        private static void Construct<T>(IEntityBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string RootsInTwoNamespaces = Usings + @"
namespace First {
    public class Service {}
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
    }
}
namespace Second {
    public class Service {}
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
    }
}";
    }
}
