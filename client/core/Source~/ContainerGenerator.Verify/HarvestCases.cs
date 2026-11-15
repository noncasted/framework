using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator.Verify {
    // Cases of docs/audits/2026-11-16_client.md V1 and m1 (E4): the harvested copy of an installer with local functions
    // binds the members of its original class and reports its diagnostics at the original syntax.
    internal static partial class Program {
        private static void RunHarvestCases() {
            Run("V1 harvest keeps an unqualified call of a sibling installer", () => RunScenario(V1SiblingCall));
            Run("V1 harvest keeps a call of a private extension of its class", () => RunScenario(V1PrivateExtension));
            Run("V1 guard: a sibling call qualified by the type is kept", () => RunScenario(V1QualifiedCall));
            Run("m1 a diagnostic of a harvested registration points to the original file", TestHarvestDiagnosticLocation);
        }

        private static void TestHarvestDiagnosticLocation() {
            var run = GenerateWithRuntime(M1HarvestMissing);
            var error = RequireId(run.Diagnostics, "CINGR003");
            var path = error.Location.GetLineSpan().Path;
            Require(path == "Fixture.cs", "CINGR003 must point to the installer, not to the harvest copy: " + error);
            Require(run.Diagnostics.Any(d => d.Location.GetLineSpan().Path.IndexOf("Harvest", StringComparison.Ordinal) >= 0) == false,
                "no diagnostic may point to the harvest copy:\n" + string.Join("\n", run.Diagnostics.Select(d => d.ToString())));
        }

        private const string V1Classes = @"
    public class A { public A() { Trace.Add(""A""); } }
    public class B { public B() { Trace.Add(""B""); } }
    public class C { public C() { Trace.Add(""C""); } }
    public class D { public D() { Trace.Add(""D""); } }
";

        private const string V1SiblingCall = Usings + "namespace Sample {" + V1Classes + @"
    public static class Installer {
        public static void AddFeature(this IScopeBuilder builder) {
            Locals();
            Helper(builder);

            void Locals() {
                builder.Register<A>();
                builder.Register<B>();
                builder.Register<C>();
            }
        }

        private static void Helper(IScopeBuilder builder) { builder.Register<D>(); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddFeature(); }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Construct);
            Check.Equal(""A,B,C,D"", Trace.Text, ""the sibling installer is a part of the scope"");
        }
    }
}";

        private const string V1PrivateExtension = Usings + "namespace Sample {" + V1Classes + @"
    public static class Installer {
        public static void AddFeature(this IScopeBuilder builder) {
            Locals();
            builder.Configure();

            void Locals() {
                builder.Register<A>();
                builder.Register<B>();
                builder.Register<C>();
            }
        }

        private static void Configure(this IScopeBuilder builder) { builder.Register<D>(); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddFeature(); }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Construct);
            Check.Equal(""A,B,C,D"", Trace.Text, ""the private extension is a part of the scope"");
        }
    }
}";

        private const string V1QualifiedCall = Usings + "namespace Sample {" + V1Classes + @"
    public static class Installer {
        public static void AddFeature(this IScopeBuilder builder) {
            Locals();
            Installer.Helper(builder);

            void Locals() {
                builder.Register<A>();
                builder.Register<B>();
                builder.Register<C>();
            }
        }

        public static void Helper(IScopeBuilder builder) { builder.Register<D>(); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddFeature(); }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Construct);
            Check.Equal(""A,B,C,D"", Trace.Text, ""the qualified sibling installer is a part of the scope"");
        }
    }
}";

        private const string M1HarvestMissing = Usings + "namespace Sample {" + V1Classes + @"
    public interface IMissing {}
    public class NeedsMissing { public NeedsMissing(IMissing missing) {} }
    public static class Installer {
        public static void AddFeature(this IScopeBuilder builder) {
            Locals();

            void Locals() {
                builder.Register<A>();
                builder.Register<B>();
                builder.Register<NeedsMissing>();
            }
        }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddFeature(); }
    }
}";
    }
}
