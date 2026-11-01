using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator.Verify {
    // Cases of docs/audits/2026-11-01_client.md, G1-G10. Written before the fixes. "guard" cases pinned what already
    // worked and the fixes must keep; the others failed for the reason of the finding.
    internal static partial class Program {
        private static void RunAuditCases() {
            Run("G1 two independent ifs that both ran create both objects", () => RunScenario(G1Independent));
            Run("G1 guard: two independent ifs, one ran, the consumer takes it", () => RunScenario(G1IndependentOneRan));
            Run("G1 two registrations inside one branch are both created", () => RunScenario(G1OneBranch));
            Run("G1 guard: if / else if / else builds only the chosen branch", () => RunScenario(G1ElseIf));
            Run("G1 guard: switch sections build only the chosen one", () => RunScenario(G1Switch));
            Run("G1 a plain registration stays when a later if did not run", () => RunScenario(G1PlainThenIf));

            Run("G2 [Inject] only on the first branch is called", () => RunScenario(G2InjectFirst));
            Run("G2 [Inject] only on the last branch is called", () => RunScenario(G2InjectLast));
            Run("G2 IDisposable only on the first branch is disposed", () => RunScenario(G2DisposeFirst));
            Run("G2 IDisposable only on the last branch does not break the other", () => RunScenario(G2DisposeLast));
            Run("G2 a service of one branch resolves from a child scope", () => RunScenario(G2ChildScope));
            Run("G2 a service of one branch is taken by a consumer of the scope", () => RunScenario(G2ConsumerOfOneBranch));
            Run("G2 a dependency of the first branch is built before the group", () => RunScenario(G2DependencyOrder));

            Run("G3 transient alternative: a parameter takes the branch that ran", () => RunScenario(G3Parameter));
            Run("G3 transient alternative: Resolve creates the branch that ran", () => RunScenario(G3Resolve));
            Run("G3 transient alternative: ResolveAll creates only the branch that ran", () => RunScenario(G3ResolveAll));
            Run("G3 singleton and transient branches of one if", () => RunScenario(G3Mixed));

            Run("G4 As through a local goes to its registration", () => RunScenario(G4AsLocal));
            Run("G4 WithParameter through a local goes to its registration", () => RunScenario(G4ParameterLocal));
            Run("G4 As through a local of an installer call", () => RunScenario(G4AsCallLocal));
            Run("G4 local function call keeps its place in the scope order", () => RunScenario(G4LocalFunctionOrder));
            Run("G4 local function call keeps its place in an installer", () => RunScenario(G4LocalFunctionInstallerOrder));

            Run("G5 WithParameter wins over a registration of the type", () => RunScenario(G5Wins));
            Run("G5 WithParameter on an installer call wins over a registration", () => RunScenario(G5OnCall));
            Run("G5 WithParameter of a collection is not replaced by an empty list", () => RunScenario(G5Collection));
            Run("G5 WithParameter from another assembly wins over a registration", () => RunScenario(G5ForeignInstaller, G5ForeignRoot));
            Run("G5 WithParameter of a generic installer wins over a registration", () => RunScenario(G5Generic));
            Run("G5 WithParameter in an if branch", () => RunScenario(G5Branch));
            Run("G5 WithParameter wins for a transient too", () => RunScenario(G5Transient));
            Run("G5 CINGR003 for a parameter WithParameter does not give", TestG5Unresolved);
            Run("G5 WithParameter of a lifetime is what the class gets", () => RunScenario(G5Lifetime));
            Run("G5 WithParameter of a read-only lifetime is what the class gets", () => RunScenario(G5ReadOnlyLifetime));
            Run("G5 WithParameter of ILifetime goes to a parameter of IReadOnlyLifetime", () => RunScenario(G5LifetimeToReadOnly));
            Run("G5 WithParameter of a class goes to a parameter of its interface over a registration", () => RunScenario(G5ClassToInterface));
            Run("G5 WithParameter of a list goes to a parameter of IReadOnlyList", () => RunScenario(G5ListToReadOnlyList));
            Run("G5 WithParameter of an array goes to a parameter of IReadOnlyList", () => RunScenario(G5ArrayToReadOnlyList));
            Run("G5 WithParameter of a switch arm goes to a parameter of its interface", () => RunScenario(G5ArmAssignable));
            Run("G5 guard: the exact type wins over an assignable one", () => RunScenario(G5ExactWins));
            Run("G5 CINGR014 for two WithParameter assignable to one parameter", TestG5Ambiguous);
            Run("G5 CINGR015 warning for a WithParameter no parameter can take", TestG5Unused);
            Run("G5 CINGR015 warning for a WithParameter of a switch arm no parameter can take", TestG5UnusedArm);
            Run("G5 guard: no CINGR015 for a WithParameter a parameter takes", TestG5UsedNoWarning);
            Run("G5 no CINGR015 for switch arms with parameters of their own types", TestG5ArmsOwnTypes);
            Run("G5 guard: no CINGR015 for a WithParameter taken by [Inject]", TestG5InjectNoWarning);
            Run("G5 WithParameter of a container is what the class gets", () => RunScenario(G5Container));
            Run("G5 guard: without WithParameter the class gets the lifetime and the container of the scope", () => RunScenario(G5ImplicitStays));
            Run("G5 WithParameter of a lifetime wins over a registration of the type", () => RunScenario(G5LifetimeOverRegistration));
            Run("G5 guard: WithParameter wins over a registration written after it", () => RunScenario(G5RegistrationAfter));
            Run("G5 WithParameter of a switch arm wins over a registration written after the switch", () => RunScenario(G8SwitchInstaller, G5ArmRegisteredAfterRoot));
            Run("G5 guard: WithParameter of a lifetime on a switch arm", () => RunScenario(G5ArmLifetime));

            Run("G6 a const variable gives the lifetime", () => RunScenario(G6ConstVariable));
            Run("G6 a static call of the extension gives the lifetime", () => RunScenario(G6StaticCall));
            Run("G6 guard: a named argument gives the lifetime", () => RunScenario(G6Named));
            Run("G6 the text of the argument does not give the lifetime", () => RunScenario(G6Text));
            Run("G6 CINGR001 for a lifetime in a variable", () => RequireLifetimeError(G6Variable, "ServiceLifetime"));
            Run("G6 CINGR001 for a lifetime in a parameter of an installer", () => RequireLifetimeError(G6Parameter, "lifetime"));
            Run("G6 CINGR001 for a transient switch arm", () => RequireLifetimeError(G6TransientArm, "switch"));
            Run("G6 guard: a singleton switch arm is accepted", () => RunScenario(G6SingletonArm));

            Run("G7 overloads of an installer keep their own bodies", () => RunScenario(G7Overloads));
            Run("G7 a forwarding overload is not a call of itself", () => RunScenario(G7Forwarding));
            Run("G7 overloads called from another assembly", () => RunScenario(G7ForeignInstaller, G7ForeignRoot));
            Run("G7 a public overload forwarding to a private one from another assembly", () => RunScenario(G7PrivateInstaller, G7PrivateRoot));
            Run("G7 generic overloads keep their own bodies", () => RunScenario(G7Generic));
            Run("G7 an attributed root with an installer overload", () => RunScenario(G7RootAndInstaller));
            Run("G7 CINGR013 for an overloaded root", TestG7OverloadedRoot);
            Run("G7 CINGR013 for an overloaded entity root", TestG7OverloadedEntityRoot);

            Run("G8 IEnumerable parameter with no registration", () => RunScenario(G8EnumerableEmpty));
            Run("G8 IEnumerable parameter with registrations", () => RunScenario(G8EnumerableFull));
            Run("G8 IReadOnlyCollection parameter", () => RunScenario(G8ReadOnlyCollection));
            Run("G8 guard: WithParameter of a switch arm in one assembly", () => RunScenario(G8SwitchInstaller + G8SwitchRoot.Replace(Usings, "")));
            Run("G8 WithParameter of a switch arm from another assembly", () => RunScenario(G8SwitchInstaller, G8SwitchRoot));
            Run("G8 WithParameter of a switch arm wins over a registration from another assembly", () => RunScenario(G8SwitchInstaller, G8SwitchRegisteredRoot));

            Run("G9a a game assembly named Systems.Battle is generated", () => TestG9Assembly("Systems.Battle", true));
            Run("G9a a game assembly named UnityRunner is generated", () => TestG9Assembly("UnityRunner", true));
            Run("G9a a game assembly named MicrosoftFlight is generated", () => TestG9Assembly("MicrosoftFlight", true));
            Run("G9a guard: System.Foo is still skipped", () => TestG9Assembly("System.Foo", false));
            Run("G9b two view types with Local in the name both get a class", TestG9Variants);
            Run("G9b guard: Local and Remote keep their names", TestG9VariantNames);
            Run("G9c ScopePlan throws on a root missing from the document", TestG9UnknownRoot);
            Run("G9d a manifest of another blob version is reported", TestG9BlobVersion);
            Run("G9e CINGR010 for an internal [Inject] of another assembly", () => TestG9HiddenInject(G9InternalInject));
            Run("G9e CINGR010 for an internal [Inject] on a base class of another assembly", () => TestG9HiddenInject(G9BaseInternalInject));
            Run("G9e guard: a public [Inject] of another assembly is called", () => RunScenario(G9PublicInject, G9InjectRoot));

            Run("G10 a foreign AddSnapshotHandler is an ordinary installer", () => RunG10("AddSnapshotHandler"));
            Run("G10 a foreign AddCardActionSyncResolver is an ordinary installer", () => RunG10("AddCardActionSyncResolver"));
            Run("G10 guard: AddSnapshotHandler in one assembly", () => RunScenario(G10Installer.Replace("NAME", "AddSnapshotHandler") + G10Root.Replace(Usings, "").Replace("NAME", "AddSnapshotHandler")));
        }

        private static void TestG5Unresolved() {
            var run = GenerateWithRuntime(G5Unresolved);
            var error = RequireId(run.Diagnostics, "CINGR003");
            Require(error.GetMessage().IndexOf("Config", StringComparison.Ordinal) >= 0, "CINGR003 must name the parameter: " + error.GetMessage());
        }

        private static void RequireLifetimeError(string source, string fragment) {
            var run = GenerateWithRuntime(source);
            var error = RequireId(run.Diagnostics, "CINGR001");
            Require(error.GetMessage().IndexOf(fragment, StringComparison.Ordinal) >= 0, "CINGR001 must contain '" + fragment + "': " + error.GetMessage());
        }

        private static void TestG5Ambiguous() {
            var run = GenerateWithRuntime(G5Ambiguous);
            var error = RequireId(run.Diagnostics, "CINGR014");
            Require(error.Severity == DiagnosticSeverity.Error, "CINGR014 severity " + error.Severity);
        }

        private static void TestG5Unused() {
            var run = GenerateWithRuntime(G5Unused);
            var warning = RequireId(run.Diagnostics, "CINGR015");
            Require(warning.Severity == DiagnosticSeverity.Warning, "CINGR015 severity " + warning.Severity);
            Require(warning.GetMessage().IndexOf("Sample.Foo", StringComparison.Ordinal) >= 0 &&
                    warning.GetMessage().IndexOf("Sample.Other", StringComparison.Ordinal) >= 0,
                "CINGR015 must name the class and the type: " + warning.GetMessage());
            Require(run.Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error), "a warning only");
        }

        private static void TestG5UnusedArm() {
            var run = GenerateWithRuntime(G5UnusedArm);
            var warning = RequireId(run.Diagnostics, "CINGR015");
            Require(warning.Severity == DiagnosticSeverity.Warning, "CINGR015 severity " + warning.Severity);
            Require(warning.GetMessage().IndexOf("Sample.A", StringComparison.Ordinal) >= 0, "CINGR015 must name the arm: " + warning.GetMessage());
            Require(run.Diagnostics.Count(d => d.Id == "CINGR015") == 1, "only the arm that cannot take it");
        }

        private static void TestG5ArmsOwnTypes() {
            var run = GenerateWithRuntime(G5ArmsOwnTypes);
            Require(run.Diagnostics.All(d => d.Id != "CINGR015"), "each arm takes its own value: " +
                string.Join("; ", run.Diagnostics.Where(d => d.Id == "CINGR015").Select(d => d.GetMessage())));
        }

        private static void TestG5UsedNoWarning() {
            foreach (var source in new[] { G5LifetimeToReadOnly, G5ClassToInterface, G5ArrayToReadOnlyList, G5ArmAssignable, G8SwitchInstaller + G8SwitchRoot.Replace(Usings, "") }) {
                var run = GenerateWithRuntime(source);
                Require(run.Diagnostics.All(d => d.Id != "CINGR015"), "a taken value is not reported");
            }
        }

        private static void TestG5InjectNoWarning() {
            var run = GenerateWithRuntime(G5TakenByInject);
            Require(run.Diagnostics.All(d => d.Id != "CINGR015"), "a value taken by [Inject] is not reported");
        }

        private static void TestG7OverloadedRoot() {
            var run = GenerateWithRuntime(G7OverloadedRoot);
            var error = RequireId(run.Diagnostics, "CINGR013");
            Require(error.Severity == DiagnosticSeverity.Error, "CINGR013 severity " + error.Severity);
            Require(error.GetMessage().IndexOf("Sample.OverloadedRoot.Construct", StringComparison.Ordinal) >= 0, "CINGR013 must name the root: " + error.GetMessage());
        }

        private static void TestG7OverloadedEntityRoot() {
            var run = GenerateWithRuntime(G7OverloadedEntityRoot);
            RequireId(run.Diagnostics, "CINGR013");
        }

        private static void TestG9Assembly(string assembly, bool generated) {
            var run = GenerateWithRuntime(G9Assembly, assembly);
            var has = run.Output.SyntaxTrees.Any(t => t.GetText().ToString().IndexOf("class PlainRootConstructContainer", StringComparison.Ordinal) >= 0);
            Require(has == generated, assembly + (generated ? " must get a generated class" : " must be skipped"));
        }

        private static void TestG9Variants() {
            var run = GenerateWithRuntime(G9Variants);
            RequireNoGeneratorErrors(run);
            RequireGeneratedContains(run.Output, "typeof(global::Sample.LocalPlayerView)");
            RequireGeneratedContains(run.Output, "typeof(global::Sample.LocalEnemyView)");
            EmitImage(run.Output, "G9Variants");
        }

        private static void TestG9VariantNames() {
            var run = GenerateWithRuntime(G9VariantNames);
            RequireNoGeneratorErrors(run);
            RequireGeneratedContains(run.Output, "class VariantFactoryLocalLocalContainer");
            RequireGeneratedContains(run.Output, "class VariantFactoryRemoteRemoteContainer");
        }

        private static void TestG9UnknownRoot() {
            var compilation = Compile(G9Assembly, "Scenario", new[] { RuntimeReference() }, includeStubs: false);
            var references = ReferenceSymbols.Create(compilation)!;
            var document = new GraphWalker(compilation, references).Walk();
            var thrown = false;
            try {
                ScopePlanBuilder.Build(new ScopeGraph { RootId = "Sample.Unknown.Construct" }, document, compilation, references);
            }
            catch (InvalidOperationException) {
                thrown = true;
            }

            Require(thrown, "a root that is not in the document must throw, not skip the scope silently");
        }

        private static void TestG9BlobVersion() {
            var foreign = EmitPe(Compile(G9OldManifest, "OldManifest", new[] { RuntimeReference() }, includeStubs: false), "OldManifest");
            var run = GenerateWithRuntime(G9OldManifestRoot, "Scenario", foreign);
            var error = RequireId(run.Diagnostics, "CINGR001");
            Require(error.GetMessage().IndexOf("OldManifest", StringComparison.Ordinal) >= 0, "CINGR001 must name the assembly: " + error.GetMessage());
        }

        private static void TestG9HiddenInject(string foreignSource) {
            var foreign = RunGenerator(Compile(foreignSource, "Foreign", new[] { RuntimeReference() }, includeStubs: false), "Foreign");
            var run = GenerateWithRuntime(G9InjectRoot, "Scenario", foreign.Reference);
            var error = RequireId(run.Diagnostics, "CINGR010");
            Require(error.GetMessage().IndexOf("Setup", StringComparison.Ordinal) >= 0, "CINGR010 must name the method: " + error.GetMessage());
        }

        private static void RunG10(string name) {
            RunScenario(G10Installer.Replace("NAME", name), G10Root.Replace("NAME", name));
        }

        private const string Usings = @"
using Internal;
using System;
using System.Collections.Generic;
using System.Linq;
";

        private const string G1Independent = Usings + @"
namespace Sample {
    public class A : IScopeSetup { public A() { Trace.Add(""A""); } public void OnSetup(IReadOnlyLifetime lifetime) {} }
    public class B : IScopeSetup { public B() { Trace.Add(""B""); } public void OnSetup(IReadOnlyLifetime lifetime) {} }
    public static class Root {
        public static bool First, Second;
        public static void Construct(IScopeBuilder builder) {
            if (First) builder.Register<A>().As<IScopeSetup>();
            if (Second) builder.Register<B>().As<IScopeSetup>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.First = Root.Second = true;
            var container = TestHost.Build(Root.Construct);
            Check.Equal(""A,B"", Trace.Text, ""both ifs ran: both objects must be created"");
            Check.Equal(""2"", container.ResolveAll<IScopeSetup>().Count.ToString(), ""both objects listen to the phase"");
        }
    }
}";

        private const string G1IndependentOneRan = Usings + @"
namespace Sample {
    public interface IService {}
    public class A : IService {}
    public class B : IService {}
    public class Consumer { public readonly IService Service; public Consumer(IService service) { Service = service; } }
    public static class Root {
        public static bool First, Second;
        public static void Construct(IScopeBuilder builder) {
            if (First) builder.Register<A>().As<IService>();
            if (Second) builder.Register<B>().As<IService>();
            builder.Register<Consumer>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.First = true; Root.Second = false;
            var first = TestHost.Build(Root.Construct);
            Check.True(first.Resolve<Consumer>().Service is A, ""only the first if ran"");
            Check.True(first.Resolve<IService>() is A, ""Resolve: only the first if ran"");
            Root.First = false; Root.Second = true;
            var second = TestHost.Build(Root.Construct);
            Check.True(second.Resolve<Consumer>().Service is B, ""only the second if ran"");
            Check.True(second.Resolve<IService>() is B, ""Resolve: only the second if ran"");
        }
    }
}";

        private const string G1OneBranch = Usings + @"
namespace Sample {
    public class A : IScopeSetup { public A() { Trace.Add(""A""); } public void OnSetup(IReadOnlyLifetime lifetime) {} }
    public class B : IScopeSetup { public B() { Trace.Add(""B""); } public void OnSetup(IReadOnlyLifetime lifetime) {} }
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            if (Flag) {
                builder.Register<A>().As<IScopeSetup>();
                builder.Register<B>().As<IScopeSetup>();
            }
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = true;
            TestHost.Build(Root.Construct);
            Check.Equal(""A,B"", Trace.Text, ""one branch registers two objects: both run"");
        }
    }
}";

        private const string G1ElseIf = Usings + @"
namespace Sample {
    public interface IService {}
    public class A : IService { public A() { Trace.Add(""A""); } }
    public class B : IService { public B() { Trace.Add(""B""); } }
    public class C : IService { public C() { Trace.Add(""C""); } }
    public static class Root {
        public static int Kind;
        public static void Construct(IScopeBuilder builder) {
            if (Kind == 0) builder.Register<A>().As<IService>();
            else if (Kind == 1) builder.Register<B>().As<IService>();
            else builder.Register<C>().As<IService>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Kind = 1;
            var container = TestHost.Build(Root.Construct);
            Check.Equal(""B"", Trace.Text, ""only the chosen branch"");
            Check.True(container.Resolve<IService>() is B, ""Resolve gives it"");
            Check.Equal(""1"", container.ResolveAll<IService>().Count.ToString(), ""ResolveAll gives it"");
        }
    }
}";

        private const string G1Switch = Usings + @"
namespace Sample {
    public interface IService {}
    public class A : IService { public A() { Trace.Add(""A""); } }
    public class B : IService { public B() { Trace.Add(""B""); } }
    public static class Root {
        public static int Kind;
        public static void Construct(IScopeBuilder builder) {
            switch (Kind) {
                case 0: builder.Register<A>().As<IService>(); break;
                default: builder.Register<B>().As<IService>(); break;
            }
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Kind = 0;
            var container = TestHost.Build(Root.Construct);
            Check.Equal(""A"", Trace.Text, ""only the chosen section"");
            Check.True(container.Resolve<IService>() is A, ""Resolve gives it"");
        }
    }
}";

        private const string G1PlainThenIf = Usings + @"
namespace Sample {
    public interface IService {}
    public class Plain : IService {}
    public class Extra : IService {}
    public class Consumer { public readonly IService Service; public Consumer(IService service) { Service = service; } }
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Plain>().As<IService>();
            if (Flag) builder.Register<Extra>().As<IService>();
            builder.Register<Consumer>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = false;
            var plain = TestHost.Build(Root.Construct);
            Check.True(plain.Resolve<Consumer>().Service is Plain, ""the if did not run: the plain registration is the last one"");
            Check.True(plain.Resolve<IService>() is Plain, ""Resolve: the plain registration"");
            Root.Flag = true;
            var extra = TestHost.Build(Root.Construct);
            Check.True(extra.Resolve<Consumer>().Service is Extra, ""the if ran: the extra registration is the last one"");
        }
    }
}";

        private const string G2InjectFirst = Usings + @"
namespace Sample {
    public class Dep {}
    public interface IService {}
    public class First : IService { [Inject] public void Setup(Dep dep) { Trace.Add(""First.Setup""); } }
    public class Last : IService {}
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Dep>();
            if (Flag) builder.Register<First>().As<IService>();
            else builder.Register<Last>().As<IService>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = true;
            TestHost.Build(Root.Construct);
            Check.Equal(""First.Setup"", Trace.Text, ""[Inject] of the branch that ran"");
        }
    }
}";

        private const string G2InjectLast = Usings + @"
namespace Sample {
    public class Dep {}
    public interface IService {}
    public class First : IService {}
    public class Last : IService { [Inject] public void Setup(Dep dep) { Trace.Add(""Last.Setup""); } }
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Dep>();
            if (Flag) builder.Register<First>().As<IService>();
            else builder.Register<Last>().As<IService>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = false;
            TestHost.Build(Root.Construct);
            Check.Equal(""Last.Setup"", Trace.Text, ""[Inject] of the last branch"");
            Trace.Clear();
            Root.Flag = true;
            TestHost.Build(Root.Construct);
            Check.Equal("""", Trace.Text, ""the other branch has no [Inject]"");
        }
    }
}";

        private const string G2DisposeFirst = Usings + @"
namespace Sample {
    public interface IService {}
    public class First : IService, IDisposable { public void Dispose() { Trace.Add(""First.Dispose""); } }
    public class Last : IService {}
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            if (Flag) builder.Register<First>().As<IService>();
            else builder.Register<Last>().As<IService>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = true;
            TestHost.Build(Root.Construct).Dispose();
            Check.Equal(""First.Dispose"", Trace.Text, ""the branch that ran is disposable"");
        }
    }
}";

        private const string G2DisposeLast = Usings + @"
namespace Sample {
    public interface IService {}
    public class First : IService {}
    public class Last : IService, IDisposable { public void Dispose() { Trace.Add(""Last.Dispose""); } }
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            if (Flag) builder.Register<First>().As<IService>();
            else builder.Register<Last>().As<IService>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = true;
            TestHost.Build(Root.Construct).Dispose();
            Check.Equal("""", Trace.Text, ""the branch that ran is not disposable"");
            Root.Flag = false;
            TestHost.Build(Root.Construct).Dispose();
            Check.Equal(""Last.Dispose"", Trace.Text, ""the disposable branch"");
        }
    }
}";

        private const string G2ChildScope = Usings + @"
namespace Sample {
    public interface IShared {}
    public interface IOnlyFirst {}
    public class First : IShared, IOnlyFirst {}
    public class Last : IShared {}
    public class Needs { public readonly IOnlyFirst Value; public Needs(IOnlyFirst value) { Value = value; } }
    public static class ParentRoot {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            if (Flag) builder.Register<First>().As<IShared>().As<IOnlyFirst>();
            else builder.Register<Last>().As<IShared>();
        }
    }
    public static class ChildRoot {
        [ContainerScopeParent(typeof(ParentRoot), nameof(ParentRoot.Construct))]
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Needs>();
        }
    }
    public static class Scenario {
        public static void Run() {
            ParentRoot.Flag = true;
            var parent = TestHost.Build(ParentRoot.Construct);
            Check.True(parent.Resolve<IOnlyFirst>() is First, ""the parent exports a service of the branch that ran"");
            Check.True(parent.Resolve<First>() is First, ""the parent exports the implementation"");
            var child = TestHost.Build(ChildRoot.Construct, parent);
            Check.True(child.Resolve<Needs>().Value is First, ""the child takes it from the parent"");
        }
    }
}";

        private const string G2ConsumerOfOneBranch = Usings + @"
namespace Sample {
    public interface IShared {}
    public interface IOnlyFirst {}
    public class First : IShared, IOnlyFirst {}
    public class Last : IShared {}
    public class Needs { public readonly IOnlyFirst Value; public Needs(IOnlyFirst value) { Value = value; } }
    public class Lists { public readonly IReadOnlyList<IOnlyFirst> Values; public Lists(IReadOnlyList<IOnlyFirst> values) { Values = values; } }
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            if (Flag) builder.Register<First>().As<IShared>().As<IOnlyFirst>();
            else builder.Register<Last>().As<IShared>();
            builder.Register<Lists>();
            builder.Register<Needs>(ServiceLifetime.Transient);
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = true;
            var container = TestHost.Build(Root.Construct);
            Check.True(container.Resolve<Needs>().Value is First, ""the branch that gives the service ran"");
            Check.Equal(""1"", container.Resolve<Lists>().Values.Count.ToString(), ""the list has it"");
            Root.Flag = false;
            var other = TestHost.Build(Root.Construct);
            Check.Equal(""0"", other.Resolve<Lists>().Values.Count.ToString(), ""the other branch does not give the service"");
            Check.Throws(() => other.Resolve<Needs>(), ""IOnlyFirst"", ""no branch gave the service"");
        }
    }
}";

        // The consumer is registered first: its target (the last branch) used to be built before the dependency of the first one.
        private const string G2DependencyOrder = Usings + @"
namespace Sample {
    public interface IService {}
    public class Dep {}
    public class First : IService { public readonly Dep Dep; public First(Dep dep) { Dep = dep; } }
    public class Last : IService {}
    public class Consumer { public readonly IService Service; public Consumer(IService service) { Service = service; } }
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Consumer>();
            if (Flag) builder.Register<First>().As<IService>();
            else builder.Register<Last>().As<IService>();
            builder.Register<Dep>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = true;
            var container = TestHost.Build(Root.Construct);
            Check.True(((First)container.Resolve<Consumer>().Service).Dep != null, ""the dependency of the branch is built before it"");
        }
    }
}";

        private const string G3Common = @"
    public interface IService {}
    public class DebugService : IService { public DebugService() { Trace.Add(""Debug""); } }
    public class PlainService : IService { public PlainService() { Trace.Add(""Plain""); } }
    public static class Root {
        public static bool Debug;
        public static void Construct(IScopeBuilder builder) {
            if (Debug) builder.Register<DebugService>(ServiceLifetime.Transient).As<IService>();
            else builder.Register<PlainService>(ServiceLifetime.Transient).As<IService>();
            builder.Register<Consumer>();
        }
    }
";

        private const string G3Parameter = Usings + "namespace Sample {" + G3Common + @"
    public class Consumer { public readonly IService Service; public Consumer(IService service) { Service = service; } }
    public static class Scenario {
        public static void Run() {
            Root.Debug = true;
            Check.True(TestHost.Build(Root.Construct).Resolve<Consumer>().Service is DebugService, ""the debug branch ran"");
            Root.Debug = false;
            Check.True(TestHost.Build(Root.Construct).Resolve<Consumer>().Service is PlainService, ""the plain branch ran"");
        }
    }
}";

        private const string G3Resolve = Usings + "namespace Sample {" + G3Common + @"
    public class Consumer {}
    public static class Scenario {
        public static void Run() {
            Root.Debug = true;
            var container = TestHost.Build(Root.Construct);
            Check.True(container.Resolve<IService>() is DebugService, ""the debug branch ran"");
            Check.True(container.Resolve<IService>() != container.Resolve<IService>(), ""a transient: a new one each time"");
            Check.True(container.TryResolve(typeof(PlainService), out _) == false, ""the other branch is not resolved"");
        }
    }
}";

        private const string G3ResolveAll = Usings + "namespace Sample {" + G3Common + @"
    public class Consumer {}
    public static class Scenario {
        public static void Run() {
            Root.Debug = true;
            var all = TestHost.Build(Root.Construct).ResolveAll<IService>();
            Check.Equal(""1"", all.Count.ToString(), ""only the branch that ran"");
            Check.True(all[0] is DebugService, ""the debug branch ran"");
            Check.Equal(""Debug"", Trace.Text, ""the other branch is not created"");
        }
    }
}";

        private const string G3Mixed = Usings + @"
namespace Sample {
    public interface IService {}
    public class Single : IService { public Single() { Trace.Add(""Single""); } }
    public class Many : IService { public Many() { Trace.Add(""Many""); } }
    public class Consumer { public readonly IService Service; public Consumer(IService service) { Service = service; } }
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            if (Flag) builder.Register<Single>().As<IService>();
            else builder.Register<Many>(ServiceLifetime.Transient).As<IService>();
            builder.Register<Consumer>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = true;
            var single = TestHost.Build(Root.Construct);
            Check.True(single.Resolve<Consumer>().Service is Single, ""the singleton branch ran"");
            Check.True(single.Resolve<IService>() == single.Resolve<IService>(), ""one instance"");
            Root.Flag = false;
            var many = TestHost.Build(Root.Construct);
            Check.True(many.Resolve<Consumer>().Service is Many, ""the transient branch ran"");
            Check.True(many.Resolve<IService>() != many.Resolve<IService>(), ""a new instance each time"");
        }
    }
}";

        private const string G4AsLocal = Usings + @"
namespace Sample {
    public interface IService {}
    public class A : IService {}
    public class C : IService {}
    public class Consumer { public readonly IService Service; public Consumer(IService service) { Service = service; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            var a = builder.Register<A>();
            var c = builder.Register<C>();
            a.As<IService>();
            builder.Register<Consumer>();
        }
    }
    public static class Scenario {
        public static void Run() {
            var container = TestHost.Build(Root.Construct);
            Check.True(container.Resolve<Consumer>().Service is A, ""the service went to the registration of the local"");
            Check.True(container.Resolve<IService>() is A, ""Resolve"");
        }
    }
}";

        private const string G4ParameterLocal = Usings + @"
namespace Sample {
    public class Config {}
    public class A { public readonly Config Config; public A(Config config) { Config = config; } }
    public class C {}
    public static class Root {
        public static readonly Config Value = new Config();
        public static void Construct(IScopeBuilder builder) {
            var a = builder.Register<A>();
            builder.Register<C>();
            a.WithParameter(Value);
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<A>().Config == Root.Value, ""the parameter went to the registration of the local"");
        }
    }
}";

        private const string G4AsCallLocal = Usings + @"
namespace Sample {
    public interface IService {}
    public class A : IService {}
    public class C : IService {}
    public class Consumer { public readonly IService Service; public Consumer(IService service) { Service = service; } }
    public static class Installer {
        public static ServiceRegistration AddA(this IScopeBuilder builder) { return builder.Register<A>(); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            var a = builder.AddA();
            builder.Register<C>();
            a.As<IService>();
            builder.Register<Consumer>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<Consumer>().Service is A, ""the service went to what the installer returned"");
        }
    }
}";

        private const string G4OrderListeners = @"
    public abstract class Listener : IScopeSetup { public void OnSetup(IReadOnlyLifetime lifetime) {} }
    public class A : Listener {}
    public class B : Listener {}
    public class L : Listener {}
    public class C : Listener {}
    public static class Names {
        public static string Of(IContainer container) { return string.Join("","", container.ResolveAll<IScopeSetup>().Select(x => x.GetType().Name)); }
    }
";

        private const string G4LocalFunctionOrder = Usings + "namespace Sample {" + G4OrderListeners + @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<A>().As<IScopeSetup>();
            builder.Register<B>().As<IScopeSetup>();
            Local(builder);
            builder.Register<C>().As<IScopeSetup>();

            void Local(IScopeBuilder inner) { inner.Register<L>().As<IScopeSetup>(); }
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""A,B,L,C"", Names.Of(TestHost.Build(Root.Construct)), ""the phase order is the order of the installer"");
        }
    }
}";

        private const string G4LocalFunctionInstallerOrder = Usings + "namespace Sample {" + G4OrderListeners + @"
    public static class Installer {
        public static void AddAll(this IScopeBuilder builder) {
            builder.Register<A>().As<IScopeSetup>();
            builder.Register<B>().As<IScopeSetup>();
            Local(builder);
            builder.Register<C>().As<IScopeSetup>();

            void Local(IScopeBuilder builder2) { builder2.Register<L>().As<IScopeSetup>(); }
        }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddAll(); }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""A,B,L,C"", Names.Of(TestHost.Build(Root.Construct)), ""the phase order is the order of the installer"");
        }
    }
}";

        private const string G5Types = @"
    public class Config { public readonly string Name; public Config(string name) { Name = name; } }
    public class Foo { public readonly Config Config; public Foo(Config config) { Config = config; } }
";

        private const string G5Wins = Usings + "namespace Sample {" + G5Types + @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance(new Config(""global""));
            builder.Register<Foo>().WithParameter(new Config(""local""));
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""local"", TestHost.Build(Root.Construct).Resolve<Foo>().Config.Name, ""the parameter of the registration"");
        }
    }
}";

        private const string G5OnCall = Usings + "namespace Sample {" + G5Types + @"
    public static class Installer {
        public static ServiceRegistration AddFoo(this IScopeBuilder builder) { return builder.Register<Foo>(); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance(new Config(""global""));
            builder.AddFoo().WithParameter(new Config(""local""));
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""local"", TestHost.Build(Root.Construct).Resolve<Foo>().Config.Name, ""the parameter of what the installer returned"");
        }
    }
}";

        private const string G5Collection = Usings + @"
namespace Sample {
    public class Item {}
    public class Foo { public readonly IReadOnlyList<Item> Items; public Foo(IReadOnlyList<Item> items) { Items = items; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter<IReadOnlyList<Item>>(new[] { new Item(), new Item() });
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""2"", TestHost.Build(Root.Construct).Resolve<Foo>().Items.Count.ToString(), ""the list given by WithParameter"");
        }
    }
}";

        private const string G5ForeignInstaller = Usings + "namespace Sample {" + G5Types + @"
    public static class Installer {
        public static void AddFoo(this IScopeBuilder builder, Config config) { builder.Register<Foo>().WithParameter(config); }
    }
}";

        private const string G5ForeignRoot = Usings + @"
namespace Sample {
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance(new Config(""global""));
            builder.AddFoo(new Config(""local""));
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""local"", TestHost.Build(Root.Construct).Resolve<Foo>().Config.Name, ""the parameter of the foreign installer"");
        }
    }
}";

        private const string G5Generic = Usings + @"
namespace Sample {
    public class Config { public readonly string Name; public Config(string name) { Name = name; } }
    public class Holder<T> { public readonly T Value; public Holder(T value) { Value = value; } }
    public static class Installer {
        public static void AddHolder<T>(this IScopeBuilder builder, T value) { builder.Register<Holder<T>>().WithParameter(value); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance(new Config(""global""));
            builder.AddHolder(new Config(""local""));
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""local"", TestHost.Build(Root.Construct).Resolve<Holder<Config>>().Value.Name, ""the parameter of the generic installer"");
        }
    }
}";

        private const string G5Branch = Usings + "namespace Sample {" + G5Types + @"
    public class Bar {}
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            if (Flag) builder.Register<Foo>().WithParameter(new Config(""branch""));
            else builder.Register<Bar>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = true;
            Check.Equal(""branch"", TestHost.Build(Root.Construct).Resolve<Foo>().Config.Name, ""the parameter of the branch that ran"");
            Root.Flag = false;
            Check.True(TestHost.Build(Root.Construct).Resolve<Bar>() != null, ""the branch with the parameter did not run: the scope builds"");
        }
    }
}";

        private const string G5Transient = Usings + "namespace Sample {" + G5Types + @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance(new Config(""global""));
            builder.Register<Foo>(ServiceLifetime.Transient).WithParameter(new Config(""local""));
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""local"", TestHost.Build(Root.Construct).Resolve<Foo>().Config.Name, ""the parameter of the transient"");
        }
    }
}";

        private const string G5Unresolved = Usings + @"
namespace Sample {
    public class Config {}
    public class Foo { public Foo(Config config, int number) {} }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(1);
        }
    }
}";

        // Entity roots of the games pass builder.ScopeLifetime and terminate what they get: the lifetime of the container.
        private const string G5Lifetime = Usings + @"
namespace Sample {
    public class Foo { public readonly ILifetime Lifetime; public Foo(ILifetime lifetime) { Lifetime = lifetime; } }
    public static class Root {
        public static readonly ILifetime Given = new Lifetime();
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(Given);
        }
    }
    public static class Scenario {
        public static void Run() {
            var container = TestHost.Build(Root.Construct);
            Check.True(container.Lifetime != Root.Given, ""the case needs another lifetime"");
            Check.True(container.Resolve<Foo>().Lifetime == Root.Given, ""the lifetime of WithParameter"");
        }
    }
}";

        private const string G5ReadOnlyLifetime = Usings + @"
namespace Sample {
    public class Foo { public readonly IReadOnlyLifetime Lifetime; public Foo(IReadOnlyLifetime lifetime) { Lifetime = lifetime; } }
    public static class Root {
        public static readonly IReadOnlyLifetime Given = new Lifetime();
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(Given);
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<Foo>().Lifetime == Root.Given, ""the lifetime of WithParameter"");
        }
    }
}";

        private const string G5LifetimeToReadOnly = Usings + @"
namespace Sample {
    public class Foo { public readonly IReadOnlyLifetime Lifetime; public Foo(IReadOnlyLifetime lifetime) { Lifetime = lifetime; } }
    public static class Root {
        public static readonly ILifetime Given = new Lifetime();
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(Given);
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<Foo>().Lifetime == Root.Given, ""the lifetime of WithParameter"");
        }
    }
}";

        private const string G5ClassToInterface = Usings + @"
namespace Sample {
    public interface IConfig { string Name { get; } }
    public class Config : IConfig { public string Name { get; } public Config(string name) { Name = name; } }
    public class Foo { public readonly IConfig Config; public Foo(IConfig config) { Config = config; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance<IConfig>(new Config(""global""));
            builder.Register<Foo>().WithParameter(new Config(""local""));
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""local"", TestHost.Build(Root.Construct).Resolve<Foo>().Config.Name, ""WithParameter, not the registration"");
        }
    }
}";

        private const string G5ListToReadOnlyList = Usings + @"
namespace Sample {
    public class Foo { public readonly IReadOnlyList<string> Names; public Foo(IReadOnlyList<string> names) { Names = names; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(new List<string> { ""local"" });
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""1"", TestHost.Build(Root.Construct).Resolve<Foo>().Names.Count.ToString(), ""the list of WithParameter, not an empty one"");
        }
    }
}";

        private const string G5ArrayToReadOnlyList = Usings + @"
namespace Sample {
    public class Foo { public readonly IReadOnlyList<string> Names; public Foo(IReadOnlyList<string> names) { Names = names; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(new[] { ""local"" });
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""1"", TestHost.Build(Root.Construct).Resolve<Foo>().Names.Count.ToString(), ""the array of WithParameter, not an empty list"");
        }
    }
}";

        private const string G5ArmAssignable = Usings + @"
namespace Sample {
    public enum Kind { First, Second }
    public interface IService { IReadOnlyLifetime Lifetime { get; } }
    public class A : IService { public IReadOnlyLifetime Lifetime { get; } public A(IReadOnlyLifetime lifetime) { Lifetime = lifetime; } }
    public class B : IService { public IReadOnlyLifetime Lifetime { get; } public B(IReadOnlyLifetime lifetime) { Lifetime = lifetime; } }
    public static class Root {
        public static readonly ILifetime Given = new Lifetime();
        public static void Construct(IScopeBuilder builder) {
            var kind = Kind.Second;
            builder.RegisterInstance(kind);
            var registration = kind switch {
                Kind.First => builder.Register<A>(),
                Kind.Second => builder.Register<B>(),
                _ => throw new InvalidOperationException(),
            };
            registration.As<IService>();
            _ = kind switch {
                Kind.First => registration.WithParameter(Given),
                Kind.Second => registration.WithParameter(Given),
                _ => throw new InvalidOperationException(),
            };
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<IService>().Lifetime == Root.Given, ""the lifetime of the arm"");
        }
    }
}";

        private const string G5ExactWins = Usings + @"
namespace Sample {
    public class Foo { public readonly IReadOnlyLifetime Lifetime; public Foo(IReadOnlyLifetime lifetime) { Lifetime = lifetime; } }
    public static class Root {
        public static readonly ILifetime Other = new Lifetime();
        public static readonly IReadOnlyLifetime Exact = new Lifetime();
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(Other).WithParameter(Exact);
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<Foo>().Lifetime == Root.Exact, ""the exact type"");
        }
    }
}";

        private const string G5Ambiguous = Usings + @"
namespace Sample {
    public interface IConfig {}
    public class First : IConfig {}
    public class Second : IConfig {}
    public class Foo { public Foo(IConfig config) {} }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(new First()).WithParameter(new Second());
        }
    }
}";

        private const string G5Unused = Usings + @"
namespace Sample {
    public class Other {}
    public class Foo {}
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(new Other());
        }
    }
}";

        private const string G5UnusedArm = Usings + @"
namespace Sample {
    public enum Kind { First, Second }
    public class Other {}
    public interface IService {}
    public class A : IService {}
    public class B : IService { public B(Other other) {} }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            var kind = Kind.Second;
            builder.RegisterInstance(kind);
            var registration = kind switch {
                Kind.First => builder.Register<A>(),
                Kind.Second => builder.Register<B>(),
                _ => throw new InvalidOperationException(),
            };
            registration.As<IService>();
            _ = kind switch {
                Kind.First => registration.WithParameter(new Other()),
                Kind.Second => registration.WithParameter(new Other()),
                _ => throw new InvalidOperationException(),
            };
        }
    }
}";

        private const string G5ArmsOwnTypes = Usings + @"
namespace Sample {
    public enum Kind { First, Second }
    public class ForA {}
    public class ForB {}
    public interface IService {}
    public class A : IService { public A(ForA value) {} }
    public class B : IService { public B(ForB value) {} }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            var kind = Kind.Second;
            builder.RegisterInstance(kind);
            var registration = kind switch {
                Kind.First => builder.Register<A>(),
                Kind.Second => builder.Register<B>(),
                _ => throw new InvalidOperationException(),
            };
            registration.As<IService>();
            _ = kind switch {
                Kind.First => registration.WithParameter(new ForA()),
                Kind.Second => registration.WithParameter(new ForB()),
                _ => throw new InvalidOperationException(),
            };
        }
    }
}";

        private const string G5TakenByInject = Usings + @"
namespace Sample {
    public class Other {}
    public class Foo { [Inject] public void Construct(Other other) {} }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(new Other());
        }
    }
}";

        private const string G5Container = Usings + @"
namespace Sample {
    public class Foo { public readonly IContainer Container; public Foo(IContainer container) { Container = container; } }
    public static class Other {
        public static void Construct(IScopeBuilder builder) {
        }
    }
    public static class Root {
        public static IContainer Given;
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(Given);
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Given = TestHost.Build(Other.Construct);
            var container = TestHost.Build(Root.Construct);
            Check.True(container != Root.Given, ""the case needs another container"");
            Check.True(container.Resolve<Foo>().Container == Root.Given, ""the container of WithParameter"");
        }
    }
}";

        private const string G5ImplicitStays = Usings + @"
namespace Sample {
    public class Given { public readonly ILifetime Lifetime; public Given(ILifetime lifetime) { Lifetime = lifetime; } }
    public class Plain {
        public readonly ILifetime Lifetime;
        public readonly IReadOnlyLifetime ReadOnly;
        public readonly IContainer Container;
        public Plain(ILifetime lifetime, IReadOnlyLifetime readOnly, IContainer container) { Lifetime = lifetime; ReadOnly = readOnly; Container = container; }
    }
    public static class Root {
        public static readonly ILifetime Value = new Lifetime();
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Given>().WithParameter(Value);
            builder.Register<Plain>();
        }
    }
    public static class Scenario {
        public static void Run() {
            var container = TestHost.Build(Root.Construct);
            var plain = container.Resolve<Plain>();
            Check.True(plain.Lifetime == container.Lifetime, ""the lifetime of the scope"");
            Check.True(plain.ReadOnly == container.Lifetime, ""the read-only lifetime of the scope"");
            Check.True(plain.Container == container, ""the container of the scope"");
        }
    }
}";

        private const string G5LifetimeOverRegistration = Usings + @"
namespace Sample {
    public class Foo { public readonly ILifetime Lifetime; public Foo(ILifetime lifetime) { Lifetime = lifetime; } }
    public class Plain { public readonly ILifetime Lifetime; public Plain(ILifetime lifetime) { Lifetime = lifetime; } }
    public static class Root {
        public static readonly ILifetime Registered = new Lifetime();
        public static readonly ILifetime Given = new Lifetime();
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance(Registered);
            builder.Register<Foo>().WithParameter(Given);
            builder.Register<Plain>();
        }
    }
    public static class Scenario {
        public static void Run() {
            var container = TestHost.Build(Root.Construct);
            Check.True(container.Resolve<Foo>().Lifetime == Root.Given, ""WithParameter, not the registration"");
            Check.True(container.Resolve<Plain>().Lifetime == Root.Registered, ""without WithParameter: the registration"");
        }
    }
}";

        private const string G5RegistrationAfter = Usings + @"
namespace Sample {
    public class Config { public readonly string Name; public Config(string name) { Name = name; } }
    public class Foo { public readonly Config Config; public Foo(Config config) { Config = config; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Foo>().WithParameter(new Config(""local""));
            builder.RegisterInstance(new Config(""global""));
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""local"", TestHost.Build(Root.Construct).Resolve<Foo>().Config.Name, ""WithParameter, not the registration"");
        }
    }
}";

        private const string G5ArmRegisteredAfterRoot = Usings + @"
namespace Sample {
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.AddService(Kind.Second);
            builder.RegisterInstance(new Config(""global""));
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""second"", TestHost.Build(Root.Construct).Resolve<IService>().Config.Name, ""the parameter of the chosen arm, not the registration"");
        }
    }
}";

        private const string G5ArmLifetime = Usings + @"
namespace Sample {
    public enum Kind { First, Second }
    public interface IService { ILifetime Lifetime { get; } }
    public class A : IService { public ILifetime Lifetime { get; } public A(ILifetime lifetime) { Lifetime = lifetime; } }
    public class B : IService { public ILifetime Lifetime { get; } public B(ILifetime lifetime) { Lifetime = lifetime; } }
    public static class Root {
        public static readonly ILifetime Given = new Lifetime();
        public static void Construct(IScopeBuilder builder) {
            var kind = Kind.Second;
            builder.RegisterInstance(kind);
            var registration = kind switch {
                Kind.First => builder.Register<A>(),
                Kind.Second => builder.Register<B>(),
                _ => throw new InvalidOperationException(),
            };
            registration.As<IService>();
            _ = kind switch {
                Kind.First => registration.WithParameter(Given),
                Kind.Second => registration.WithParameter(Given),
                _ => throw new InvalidOperationException(),
            };
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<IService>().Lifetime == Root.Given, ""the lifetime of the arm"");
        }
    }
}";

        private const string G6Types = @"
    public class A {}
    public static class Scenario {
        public static void Run() {
            var container = TestHost.Build(Root.Construct);
            Check.True(container.Resolve<A>() != container.Resolve<A>(), ""a transient: a new one each time"");
        }
    }
";

        private const string G6ConstVariable = Usings + "namespace Sample {" + G6Types + @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            const ServiceLifetime kind = ServiceLifetime.Transient;
            builder.Register<A>(kind);
        }
    }
}";

        private const string G6StaticCall = Usings + "namespace Sample {" + G6Types + @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            BuilderExtensions.Register<A>(builder, ServiceLifetime.Transient);
        }
    }
}";

        private const string G6Named = Usings + "namespace Sample {" + G6Types + @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<A>(lifetime: ServiceLifetime.Transient);
        }
    }
}";

        private const string G6Text = Usings + @"
namespace Sample {
    public class A {}
    public static class Root {
        public const ServiceLifetime NotTransient = ServiceLifetime.Singleton;
        public static void Construct(IScopeBuilder builder) {
            builder.Register<A>(NotTransient);
        }
    }
    public static class Scenario {
        public static void Run() {
            var container = TestHost.Build(Root.Construct);
            Check.True(container.Resolve<A>() == container.Resolve<A>(), ""a singleton: the same one each time"");
        }
    }
}";

        private const string G6Variable = Usings + @"
namespace Sample {
    public class A {}
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            var kind = Flag ? ServiceLifetime.Transient : ServiceLifetime.Singleton;
            builder.Register<A>(kind);
        }
    }
}";

        private const string G6Parameter = Usings + @"
namespace Sample {
    public class A {}
    public static class Installer {
        public static void AddA(this IScopeBuilder builder, ServiceLifetime kind) { builder.Register<A>(kind); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddA(ServiceLifetime.Transient); }
    }
}";

        private const string G6Arms = @"
    public enum Kind { First, Second }
    public interface IService {}
    public class A : IService {}
    public class B : IService {}
";

        private const string G6TransientArm = Usings + "namespace Sample {" + G6Arms + @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            var kind = Kind.First;
            builder.RegisterInstance(kind);
            var registration = kind switch {
                Kind.First => builder.Register<A>(ServiceLifetime.Transient),
                Kind.Second => builder.Register<B>(ServiceLifetime.Transient),
                _ => throw new InvalidOperationException(),
            };
            registration.As<IService>();
        }
    }
}";

        private const string G6SingletonArm = Usings + "namespace Sample {" + G6Arms + @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            var kind = Kind.First;
            builder.RegisterInstance(kind);
            var registration = kind switch {
                Kind.First => builder.Register<A>(ServiceLifetime.Singleton),
                Kind.Second => builder.Register<B>(),
                _ => throw new InvalidOperationException(),
            };
            registration.As<IService>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<IService>() is A, ""the chosen arm"");
        }
    }
}";

        private const string G7Installer = @"
    public class One { public One() { Trace.Add(""One""); } }
    public class Two { public Two() { Trace.Add(""Two""); } }
    public static class Installer {
        public static void AddX(this IScopeBuilder builder) { builder.Register<One>(); }
        public static void AddX(this IScopeBuilder builder, int number) { builder.Register<Two>(); }
    }
";

        private const string G7RootBody = @"
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.AddX();
            builder.AddX(1);
        }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Construct);
            Check.Equal(""One,Two"", Trace.Text, ""each overload registers its own type"");
        }
    }
";

        private const string G7Overloads = Usings + "namespace Sample {" + G7Installer + G7RootBody + "}";
        private const string G7ForeignInstaller = Usings + "namespace Sample {" + G7Installer + "}";
        private const string G7ForeignRoot = Usings + "namespace Sample {" + G7RootBody + "}";

        private const string G7Forwarding = Usings + @"
namespace Sample {
    public class Two { public Two() { Trace.Add(""Two""); } }
    public static class Installer {
        public static void AddX(this IScopeBuilder builder) => builder.AddX(1);
        public static void AddX(this IScopeBuilder builder, int number) { builder.Register<Two>(); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddX(); }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Construct);
            Check.Equal(""Two"", Trace.Text, ""the forwarding overload reaches the other one"");
        }
    }
}";

        private const string G7PrivateInstaller = Usings + @"
namespace Sample {
    public class Two { public Two() { Trace.Add(""Two""); } }
    public static class Installer {
        public static void AddX(this IScopeBuilder builder) => AddX(builder, 1);
        private static void AddX(IScopeBuilder builder, int number) { builder.Register<Two>(); }
    }
}";

        private const string G7PrivateRoot = Usings + @"
namespace Sample {
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddX(); }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Construct);
            Check.Equal(""Two"", Trace.Text, ""the public overload reaches the private one"");
        }
    }
}";

        private const string G7Generic = Usings + @"
namespace Sample {
    public class One { public One() { Trace.Add(""One""); } }
    public class Two { public Two() { Trace.Add(""Two""); } }
    public class Holder<T> { public Holder() { Trace.Add(""Holder""); } }
    public class Wrapper<T> { public Wrapper() { Trace.Add(""Wrapper""); } }
    public static class Installer {
        public static void AddX<T>(this IScopeBuilder builder) { builder.Register<Holder<T>>(); }
        public static void AddX<T>(this IScopeBuilder builder, int number) { builder.Register<Wrapper<T>>(); }
    }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.AddX<One>();
            builder.AddX<Two>(1);
        }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Construct);
            Check.Equal(""Holder,Wrapper"", Trace.Text, ""each generic overload registers its own type"");
        }
    }
}";

        private const string G7RootAndInstaller = Usings + @"
namespace Sample {
    public class Two { public Two() { Trace.Add(""Two""); } }
    public static class Root {
        [ContainerGraphRoot]
        public static void Make(IScopeBuilder builder) { Make(builder, 1); }
        public static void Make(IScopeBuilder builder, int number) { builder.Register<Two>(); }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Make);
            Check.Equal(""Two"", Trace.Text, ""the root reaches its installer overload"");
        }
    }
}";

        private const string G7OverloadedRoot = Usings + @"
namespace Sample {
    public class One {}
    public class Two {}
    public static class OverloadedRoot {
        public static void Construct(IScopeBuilder builder) { builder.Register<One>(); }
        public static void Construct(IScopeBuilder builder, int number) { builder.Register<Two>(); }
    }
}";

        private const string G7OverloadedEntityRoot = Usings + @"
namespace Sample {
    public class One {}
    public class Two {}
    public class View : IScopeEntityView {}
    public class Factory {
        private readonly IEntityScopeLoader _loader;
        private readonly View _view;
        public Factory(IEntityScopeLoader loader, View view) { _loader = loader; _view = view; }
        public void Load() { _loader.Load(null, null, _view, Install); }
        private static void Install(IEntityBuilder builder) { builder.Register<One>(); }
        private static void Install(IEntityBuilder builder, int number) { builder.Register<Two>(); }
    }
}";

        private const string G8EnumerableEmpty = Usings + @"
namespace Sample {
    public class Item {}
    public class Foo { public readonly IEnumerable<Item> Items; public Foo(IEnumerable<Item> items) { Items = items; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.Register<Foo>(); }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""0"", TestHost.Build(Root.Construct).Resolve<Foo>().Items.Count().ToString(), ""no registration: an empty list"");
        }
    }
}";

        private const string G8EnumerableFull = Usings + @"
namespace Sample {
    public interface IItem {}
    public class A : IItem {}
    public class B : IItem {}
    public class Foo { public readonly IEnumerable<IItem> Items; public Foo(IEnumerable<IItem> items) { Items = items; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<A>().As<IItem>();
            builder.Register<B>().As<IItem>();
            builder.Register<Foo>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""2"", TestHost.Build(Root.Construct).Resolve<Foo>().Items.Count().ToString(), ""both registrations"");
        }
    }
}";

        private const string G8ReadOnlyCollection = Usings + @"
namespace Sample {
    public interface IItem {}
    public class A : IItem {}
    public class Foo { public readonly IReadOnlyCollection<IItem> Items; public Foo(IReadOnlyCollection<IItem> items) { Items = items; } }
    public class Missing {}
    public class NoItems { public readonly IReadOnlyCollection<Missing> Items; public NoItems(IReadOnlyCollection<Missing> items) { Items = items; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<A>().As<IItem>();
            builder.Register<Foo>();
            builder.Register<NoItems>();
        }
    }
    public static class Scenario {
        public static void Run() {
            var container = TestHost.Build(Root.Construct);
            Check.Equal(""1"", container.Resolve<Foo>().Items.Count.ToString(), ""the registration"");
            Check.Equal(""0"", container.Resolve<NoItems>().Items.Count.ToString(), ""no registration: an empty list"");
        }
    }
}";

        private const string G8SwitchInstaller = Usings + @"
namespace Sample {
    public enum Kind { First, Second }
    public class Config { public readonly string Name; public Config(string name) { Name = name; } }
    public interface IService { Config Config { get; } }
    public class A : IService { public Config Config { get; } public A(Config config) { Config = config; } }
    public class B : IService { public Config Config { get; } public B(Config config) { Config = config; } }
    public static class Installer {
        public static void AddService(this IScopeBuilder builder, Kind kind) {
            builder.RegisterInstance(kind);
            var registration = kind switch {
                Kind.First => builder.Register<A>(),
                Kind.Second => builder.Register<B>(),
                _ => throw new InvalidOperationException(),
            };
            registration.As<IService>();
            _ = kind switch {
                Kind.First => registration.WithParameter(new Config(""first"")),
                Kind.Second => registration.WithParameter(new Config(""second"")),
                _ => throw new InvalidOperationException(),
            };
        }
    }
}
";

        private const string G8SwitchRoot = Usings + @"
namespace Sample {
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddService(Kind.Second); }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""second"", TestHost.Build(Root.Construct).Resolve<IService>().Config.Name, ""the parameter of the chosen arm"");
        }
    }
}";

        private const string G8SwitchRegisteredRoot = Usings + @"
namespace Sample {
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance(new Config(""global""));
            builder.AddService(Kind.Second);
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.Equal(""second"", TestHost.Build(Root.Construct).Resolve<IService>().Config.Name, ""the parameter of the chosen arm, not the registration"");
        }
    }
}";

        private const string G9Assembly = Usings + @"
namespace Sample {
    public class Service {}
    public static class PlainRoot {
        public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string G9Variants = Usings + @"
namespace Sample {
    public class Service {}
    public class LocalPlayerView : IScopeEntityView {}
    public class LocalEnemyView : IScopeEntityView {}
    public class VariantFactory {
        private readonly IEntityScopeLoader _loader;
        private readonly LocalPlayerView _player;
        private readonly LocalEnemyView _enemy;
        public VariantFactory(IEntityScopeLoader loader, LocalPlayerView player, LocalEnemyView enemy) { _loader = loader; _player = player; _enemy = enemy; }
        public void Load(bool isPlayer) {
            if (isPlayer) _loader.Load(null, null, _player, Build);
            else _loader.Load(null, null, _enemy, Build);
        }
        private static void Build(IEntityBuilder builder) { builder.Register<Service>(); }
    }
}";

        private const string G9VariantNames = Usings + @"
namespace Sample {
    public class Service {}
    public class ItemLocalView : IScopeEntityView {}
    public class ItemRemoteView : IScopeEntityView {}
    public class VariantFactory {
        private readonly IEntityScopeLoader _loader;
        private readonly ItemLocalView _local;
        private readonly ItemRemoteView _remote;
        public VariantFactory(IEntityScopeLoader loader, ItemLocalView local, ItemRemoteView remote) { _loader = loader; _local = local; _remote = remote; }
        public void Load(bool isLocal) {
            if (isLocal) _loader.Load(null, null, _local, Build);
            else _loader.Load(null, null, _remote, Build);
        }
        private static void Build(IEntityBuilder builder) { builder.Register<Service>(); }
    }
}";

        // Compiled without the generator: the manifest is the one an older generator wrote.
        private const string G9OldManifest = @"
using Internal;
[assembly: ContainerInstaller(""Sample.OldInstaller.AddOld"", false, """", 0, new System.Type[0], new System.Type[0], new string[0], ""v0\nparent:\n"")]
namespace Sample {
    public static class OldInstaller {
        public static void AddOld(this IScopeBuilder builder) {}
    }
}";

        private const string G9OldManifestRoot = Usings + @"
namespace Sample {
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.AddOld(); }
    }
}";

        private const string G9InternalInject = Usings + @"
namespace Sample {
    public class Dep {}
    public class Service { [Inject] internal void Setup(Dep dep) { Trace.Add(""Setup""); } }
}";

        private const string G9BaseInternalInject = Usings + @"
namespace Sample {
    public class Dep {}
    public class ServiceBase { [Inject] internal void Setup(Dep dep) { Trace.Add(""Setup""); } }
    public class Service : ServiceBase {}
}";

        private const string G9PublicInject = Usings + @"
namespace Sample {
    public class Dep {}
    public class Service { [Inject] public void Setup(Dep dep) { Trace.Add(""Setup""); } }
}";

        private const string G9InjectRoot = Usings + @"
namespace Sample {
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Dep>();
            builder.Register<Service>();
        }
    }
    public static class Scenario {
        public static void Run() {
            TestHost.Build(Root.Construct);
            Check.Equal(""Setup"", Trace.Text, ""[Inject] is called"");
        }
    }
}";

        private const string G10Installer = Usings + @"
namespace Sample {
    public interface IHandler<TData> {}
    public class Wrapper<THandler, TData> : IHandler<TData> {}
    public static class Installer {
        public static ServiceRegistration NAME<THandler, TData>(this IBuilder builder) {
            return builder.Register<Wrapper<THandler, TData>>().As<IHandler<TData>>();
        }
    }
}
";

        private const string G10Root = Usings + @"
namespace Sample {
    public class Handler {}
    public class Data {}
    public class Consumer { public readonly IHandler<Data> Value; public Consumer(IHandler<Data> value) { Value = value; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.NAME<Handler, Data>();
            builder.Register<Consumer>();
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Root.Construct).Resolve<Consumer>().Value is Wrapper<Handler, Data>, ""the body of the installer ran through its manifest"");
        }
    }
}";
    }
}
