using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator.Verify {
    // Cases of bugs found in review: each one failed before its fix.
    internal static partial class Program {
        private static void RunRegressionCases() {
            RunGenericRootCases();
            Run("WithParameter of one type is bound to its own registration", TestParameterPerRegistration);
            Run("two instances of one type get two slots", TestInstancePerRegistration);
            Run("two instances of one type reach the list in their order", () => RunScenario(InstancesInOrderScenario));
            Run("if without else builds, only its dependent throws", () => RunScenario(OptionalAlternativeScenario));
            Run("Dispose runs every step and terminates the lifetime first", TestDisposeRunsEverything);
            Run("Resolve after Dispose throws", TestResolveAfterDispose);
            Run("[Inject] of a base class is called", TestBaseClassInject);
            Run("overridden [Inject] is called once", TestOverriddenInject);
            Run("CINGR010 several [Inject] methods", TestSeveralInject);
            Run("CINGR010 generic [Inject] method", TestGenericInject);
            Run("CINGR010 private [Inject] method", TestPrivateInject);
            Run("CINGR011 abstract registered type", TestAbstractRegistration);
            Run("CINGR011 private nested registered type", TestPrivateNestedRegistration);
            Run("CINGR011 struct registered type", TestStructRegistration);
            Run("CINGR011 two constructors with the same parameter count", TestAmbiguousConstructors);
            Run("CINGR001 instead of a dropped assembly when the emitter throws", TestEmitterExceptionIsDiagnostic);
            Run("Resolve and constructor parameters take the same registration", TestDuplicateResolvesLast);
            Run("CINGR009 parameter with several registrations", TestDuplicateWarning);
            Run("CINGR012 transient phase listener", TestTransientPhaseListener);
        }

        private static void TestParameterPerRegistration() {
            var emitted = EmitRoot(ParameterPerRegistrationSource, "ParameterRoot", out _);
            Require(emitted.IndexOf("request.GetParameter<global::Sample.Config>(typeof(global::Sample.FirstConsumer), 0)", StringComparison.Ordinal) >= 0 &&
                    emitted.IndexOf("request.GetParameter<global::Sample.Config>(typeof(global::Sample.SecondConsumer), 0)", StringComparison.Ordinal) >= 0,
                "each WithParameter must be taken by its registration:\n" + emitted);
            Require(Occurrences(emitted, "request.Get<global::Sample.Config>()") == 0,
                "a WithParameter value must not be taken by type:\n" + emitted);
            RequireCompiles(emitted, ParameterPerRegistrationSource);
        }

        private static void TestInstancePerRegistration() {
            var emitted = EmitRoot(InstancePerRegistrationSource, "InstanceRoot", out _);
            Require(emitted.IndexOf("request.GetInstance<global::Sample.IDep>(0)", StringComparison.Ordinal) >= 0 &&
                    emitted.IndexOf("request.GetInstance<global::Sample.IDep>(1)", StringComparison.Ordinal) >= 0,
                "each RegisterInstance must have its own slot:\n" + emitted);
            RequireCompiles(emitted, InstancePerRegistrationSource);
        }

        private static void TestDisposeRunsEverything() {
            var emitted = EmitRoot(DisposeSource, "DisposeRoot", out _);
            var dispose = emitted.Substring(emitted.IndexOf("public void Dispose()", StringComparison.Ordinal));
            var terminate = dispose.IndexOf("_lifetime.Terminate();", StringComparison.Ordinal);
            var first = dispose.IndexOf("_firstDisposable).Dispose();", StringComparison.Ordinal);
            Require(terminate >= 0 && first > terminate, "the lifetime must end before the services are disposed:\n" + dispose);
            Require(Occurrences(dispose, "catch (global::System.Exception exception)") == 3,
                "every dispose step must be guarded:\n" + dispose);
            Require(dispose.IndexOf("global::Internal.GeneratedScopes.ThrowDisposeErrors(errors);", StringComparison.Ordinal) >= 0,
                "the errors must be rethrown:\n" + dispose);
            RequireCompiles(emitted, DisposeSource);
        }

        private static void TestResolveAfterDispose() {
            var emitted = EmitRoot(DisposeSource, "DisposeRoot", out _);
            var resolve = emitted.Substring(emitted.IndexOf("public object Resolve(", StringComparison.Ordinal));
            resolve = resolve.Substring(0, resolve.IndexOf("public T Resolve<T>", StringComparison.Ordinal));
            Require(resolve.IndexOf("throw new global::System.ObjectDisposedException(", StringComparison.Ordinal) >= 0,
                "Resolve must throw after Dispose:\n" + resolve);
        }

        private static void TestBaseClassInject() {
            var emitted = EmitRoot(BaseInjectSource, "BaseInjectRoot", out var graph);
            var derived = FindRegistration(graph, "DerivedService");
            Require(derived.InjectMethod == "Setup", "[Inject] of the base class must be found: '" + derived.InjectMethod + "'");
            Require(emitted.IndexOf("_derivedService.Setup(_config);", StringComparison.Ordinal) >= 0,
                "[Inject] of the base class must be called:\n" + emitted);
            RequireCompiles(emitted, BaseInjectSource);
        }

        private static void TestOverriddenInject() {
            var emitted = EmitRoot(OverriddenInjectSource, "OverriddenInjectRoot", out var graph);
            RequireNo(graph, "CINGR010");
            Require(Occurrences(emitted, "_derivedService.Setup(") == 1, "an overridden [Inject] is one method:\n" + emitted);
            RequireCompiles(emitted, OverriddenInjectSource);
        }

        private static void TestSeveralInject() {
            RequireBlocked(SeveralInjectSource, "SeveralInjectRoot", "CINGR010", "SetupA");
        }

        private static void TestGenericInject() {
            RequireBlocked(GenericInjectSource, "GenericInjectRoot", "CINGR010", "generic");
        }

        private static void TestPrivateInject() {
            RequireBlocked(PrivateInjectSource, "PrivateInjectRoot", "CINGR010", "not accessible");
        }

        private static void TestAbstractRegistration() {
            RequireBlocked(AbstractRegistrationSource, "AbstractRoot", "CINGR011", "abstract");
        }

        private static void TestPrivateNestedRegistration() {
            RequireBlocked(PrivateNestedSource, "PrivateNestedRoot", "CINGR011", "not accessible");
        }

        private static void TestStructRegistration() {
            RequireBlocked(StructRegistrationSource, "StructRoot", "CINGR011", "struct");
        }

        private static void TestAmbiguousConstructors() {
            RequireBlocked(AmbiguousConstructorsSource, "AmbiguousRoot", "CINGR011", "constructors");
        }

        // Two roots that get one class name make AddSource throw: the exception of the emitter used to drop every
        // generated source of the assembly with only the CS8785 warning.
        private static void TestEmitterExceptionIsDiagnostic() {
            var run = RunGenerator(Compile(EmitterExceptionSource, "EmitterException"), "EmitterException", requireEmit: false);
            RequireNoId(run.Diagnostics, "CS8785");
            var error = RequireId(run.Diagnostics, "CINGR001");
            Require(error.Severity == DiagnosticSeverity.Error, "CINGR001 severity " + error.Severity);
            Require(error.GetMessage().IndexOf("Sample.SameClass", StringComparison.Ordinal) >= 0, "CINGR001 must name the root: " + error.GetMessage());
            RequireGeneratedContains(run.Output, "class HealthyRootConstructContainer");
            RequireGeneratedContains(run.Output, "[assembly: global::Internal.ContainerInstaller(");
        }

        // The constructor parameter binds to the last registration (a transient here): Resolve must give the same one.
        private static void TestDuplicateResolvesLast() {
            var emitted = EmitRoot(DuplicateSource, "DuplicateRoot", out _);
            Require(emitted.IndexOf("{ typeof(global::Sample.IService),", StringComparison.Ordinal) < 0,
                "IService must not be exported from the singleton registered first:\n" + emitted);
            Require(emitted.IndexOf("if (type == typeof(global::Sample.IService)) {", StringComparison.Ordinal) >= 0,
                "IService must resolve to the transient registered last:\n" + emitted);
            Require(emitted.IndexOf("new global::Sample.Consumer(CreateTransientService())", StringComparison.Ordinal) >= 0,
                "the consumer takes the last registration:\n" + emitted);
            RequireCompiles(emitted, DuplicateSource);
        }

        private static void TestDuplicateWarning() {
            var graph = ResolveRoot(DuplicateSource, "DuplicateRoot");
            var warning = RequireDiagnostic(graph, "CINGR009");
            Require(warning.Descriptor.DefaultSeverity == DiagnosticSeverity.Warning, "CINGR009 severity " + warning.Descriptor.DefaultSeverity);
            var message = warning.ToDiagnostic().GetMessage();
            Require(message.IndexOf("IService", StringComparison.Ordinal) >= 0 && message.IndexOf("TransientService", StringComparison.Ordinal) >= 0,
                "CINGR009 must name the type and the registration taken: " + message);
        }

        private static void TestTransientPhaseListener() {
            RequireBlocked(TransientPhaseSource, "TransientPhaseRoot", "CINGR012", "PhaseListener");
        }

        private static void RequireBlocked(string source, string hint, string id, string fragment) {
            var compilation = Compile(source);
            var references = ReferenceSymbols.Create(compilation)!;
            var document = new GraphWalker(compilation, references).Walk();
            var graph = EdgeResolver.ResolveByHint(document, compilation, references, hint);
            Require(graph != null, "no root " + hint);
            var diagnostic = RequireDiagnostic(graph!, id);
            Require(diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error, id + " severity " + diagnostic.Descriptor.DefaultSeverity);
            Require(diagnostic.Location != null, id + " must point at the registration");
            var message = diagnostic.ToDiagnostic().GetMessage();
            Require(message.IndexOf(fragment, StringComparison.Ordinal) >= 0, id + " message must contain '" + fragment + "': " + message);
            var emitted = ScopeEmitter.TryEmit(graph!, document, compilation, references, out _, out var generated);
            Require(emitted == false, id + " must block the class:\n" + generated);
        }

        private const string ParameterPerRegistrationSource = @"
using Internal;
namespace Sample {
    public class Config {}
    public class FirstConsumer { public FirstConsumer(Config config) {} }
    public class SecondConsumer { public SecondConsumer(Config config) {} }
    public static class ParameterRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<FirstConsumer>().WithParameter(new Config());
            builder.Register<SecondConsumer>().WithParameter(new Config());
        }
    }
}
";

        private const string InstancePerRegistrationSource = @"
using Internal;
using System.Collections.Generic;
namespace Sample {
    public interface IDep {}
    public class Dep : IDep {}
    public class DepList { public DepList(IReadOnlyList<IDep> deps) {} }
    public static class InstanceRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance<IDep>(new Dep());
            builder.RegisterInstance<IDep>(new Dep());
            builder.Register<DepList>();
        }
    }
}
";

        private const string OptionalAlternativeScenario = Usings + @"
namespace Sample {
    public class Optional {}
    public class NeedsOptional { public readonly Optional Value; public NeedsOptional(Optional optional) { Value = optional; } }
    public static class Root {
        public static bool Flag;
        public static void Construct(IScopeBuilder builder) {
            if (Flag == true)
                builder.Register<Optional>();
            builder.Register<NeedsOptional>(ServiceLifetime.Transient);
        }
    }
    public static class Scenario {
        public static void Run() {
            Root.Flag = false;
            var without = TestHost.Build(Root.Construct);
            Check.Throws(() => without.Resolve<NeedsOptional>(), ""No alternative of"", ""the if did not run: the dependent throws"");
            Root.Flag = true;
            var with = TestHost.Build(Root.Construct);
            Check.True(with.Resolve<NeedsOptional>().Value != null, ""the if ran: the dependent gets it"");
        }
    }
}";

        private const string InstancesInOrderScenario = Usings + @"
namespace Sample {
    public interface IDep { string Name { get; } }
    public class Dep : IDep { public Dep(string name) { Name = name; } public string Name { get; } }
    public class DepList { public readonly IReadOnlyList<IDep> Deps; public DepList(IReadOnlyList<IDep> deps) { Deps = deps; } }
    public static class Root {
        public static void Construct(IScopeBuilder builder) {
            builder.RegisterInstance<IDep>(new Dep(""first""));
            builder.RegisterInstance<IDep>(new Dep(""second""));
            builder.Register<DepList>();
        }
    }
    public static class Scenario {
        public static void Run() {
            var deps = TestHost.Build(Root.Construct).Resolve<DepList>().Deps;
            Check.Equal(""2"", deps.Count.ToString(), ""each instance has its slot"");
            Check.Equal(""first,second"", deps[0].Name + "","" + deps[1].Name, ""the slots keep the order"");
        }
    }
}";

        private const string DisposeSource = @"
using Internal;
namespace Sample {
    public class FirstDisposable : System.IDisposable { public void Dispose() {} }
    public class SecondDisposable : System.IDisposable { public void Dispose() {} }
    public static class DisposeRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<FirstDisposable>();
            builder.Register<SecondDisposable>();
        }
    }
}
";

        private const string BaseInjectSource = @"
using Internal;
namespace Sample {
    public class Config {}
    public abstract class ServiceBase {
        [Inject]
        public void Setup(Config config) {}
    }
    public class DerivedService : ServiceBase {}
    public static class BaseInjectRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Config>();
            builder.Register<DerivedService>();
        }
    }
}
";

        private const string OverriddenInjectSource = @"
using Internal;
namespace Sample {
    public class Config {}
    public abstract class ServiceBase {
        [Inject]
        public virtual void Setup(Config config) {}
    }
    public class DerivedService : ServiceBase {
        public override void Setup(Config config) {}
    }
    public static class OverriddenInjectRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Config>();
            builder.Register<DerivedService>();
        }
    }
}
";

        private const string SeveralInjectSource = @"
using Internal;
namespace Sample {
    public class Config {}
    public class TwoInjects {
        [Inject] public void SetupA(Config config) {}
        [Inject] public void SetupB(Config config) {}
    }
    public static class SeveralInjectRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Config>();
            builder.Register<TwoInjects>();
        }
    }
}
";

        private const string GenericInjectSource = @"
using Internal;
namespace Sample {
    public class GenericInject {
        [Inject] public void Setup<T>(T value) {}
    }
    public static class GenericInjectRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<GenericInject>();
        }
    }
}
";

        private const string PrivateInjectSource = @"
using Internal;
namespace Sample {
    public class Config {}
    public class PrivateInject {
        [Inject] private void Setup(Config config) {}
    }
    public static class PrivateInjectRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Config>();
            builder.Register<PrivateInject>();
        }
    }
}
";

        private const string AbstractRegistrationSource = @"
using Internal;
namespace Sample {
    public abstract class AbstractService {}
    public static class AbstractRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<AbstractService>();
        }
    }
}
";

        private const string PrivateNestedSource = @"
using Internal;
namespace Sample {
    public static class PrivateNestedRoot {
        private class Hidden {}
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Hidden>();
        }
    }
}
";

        private const string StructRegistrationSource = @"
using Internal;
namespace Sample {
    public class Config {}
    public struct ValueService { public ValueService(Config config) {} }
    public static class StructRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Config>();
            builder.Register<ValueService>();
        }
    }
}
";

        private const string AmbiguousConstructorsSource = @"
using Internal;
namespace Sample {
    public class First {}
    public class Second {}
    public class TwoConstructors {
        public TwoConstructors(First first) {}
        public TwoConstructors(Second second) {}
    }
    public static class AmbiguousRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<First>();
            builder.Register<Second>();
            builder.Register<TwoConstructors>();
        }
    }
}
";

        private const string EmitterExceptionSource = @"
using Internal;
namespace Sample {
    public class Service {}
    public static class SameClass {
        [ContainerGraphRoot]
        public static void NameRoot(IScopeBuilder builder) {
            builder.Register<Service>();
        }
    }
    public static class SameClassName {
        [ContainerGraphRoot]
        public static void Root(IScopeBuilder builder) {
            builder.Register<Service>();
        }
    }
    public static class HealthyRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Service>();
        }
    }
}
";

        private const string DuplicateSource = @"
using Internal;
namespace Sample {
    public interface IService {}
    public class SingletonService : IService {}
    public class TransientService : IService {}
    public class Consumer { public Consumer(IService service) {} }
    public static class DuplicateRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<SingletonService>().As<IService>();
            builder.Register<TransientService>(ServiceLifetime.Transient).As<IService>();
            builder.Register<Consumer>();
        }
    }
}
";

        private const string TransientPhaseSource = @"
using Internal;
namespace Sample {
    public class PhaseListener : IScopeSetup, IScopeDispose {
        public void OnSetup(IReadOnlyLifetime lifetime) {}
        public void OnDispose() {}
    }
    public static class TransientPhaseRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.Register<PhaseListener>(ServiceLifetime.Transient).As<IScopeSetup>().As<IScopeDispose>();
        }
    }
}
";
    }
}
