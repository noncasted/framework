using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Internal.Tests
{
    // The registry, the root ids, the variants, the building and the helpers the generated classes call.
    [Category("Container")]
    public class GeneratedScopesTests
    {
        private readonly List<string> _roots = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _roots)
                GeneratedScopes.Unregister(root);

            _roots.Clear();
        }

        [Test]
        public void Register_SameFactoryMethodAgain_IsARepeatedRegistration()
        {
            var root = NewRoot();

            GeneratedScopes.Register(root, FirstFactory, NoTypes, NoTypes);
            GeneratedScopes.Register(root, FirstFactory, NoTypes, NoTypes);

            Assert.IsTrue(GeneratedScopes.IsRegistered(root));
        }

        [Test]
        public void Register_OtherFactoryMethod_Throws()
        {
            var root = NewRoot();
            GeneratedScopes.Register(root, FirstFactory, NoTypes, NoTypes);

            var exception = Assert.Throws<InvalidOperationException>(
                () => GeneratedScopes.Register(root, SecondFactory, NoTypes, NoTypes));

            StringAssert.Contains(root, exception.Message);
        }

        [Test]
        public void Register_WrongArguments_Throw()
        {
            var root = NewRoot();

            Assert.Throws<ArgumentException>(() => GeneratedScopes.Register("", FirstFactory, NoTypes, NoTypes));
            Assert.Throws<ArgumentException>(() => GeneratedScopes.Register(null, FirstFactory, NoTypes, NoTypes));
            Assert.Throws<ArgumentNullException>(() => GeneratedScopes.Register(root, null, NoTypes, NoTypes));
            Assert.Throws<ArgumentNullException>(() => GeneratedScopes.Register(root, FirstFactory, null, NoTypes));
            Assert.Throws<ArgumentNullException>(() => GeneratedScopes.Register(root, FirstFactory, NoTypes, null));
            Assert.IsFalse(GeneratedScopes.IsRegistered(root));
        }

        [Test]
        public void Unregister_RemovesTheRootOnce()
        {
            var root = NewRoot();
            GeneratedScopes.Register(root, FirstFactory, NoTypes, NoTypes);

            Assert.IsTrue(GeneratedScopes.Unregister(root));
            Assert.IsFalse(GeneratedScopes.IsRegistered(root));
            Assert.IsFalse(GeneratedScopes.Unregister(root));
        }

        [Test]
        public void EmptyRootId_IsNotRegistered()
        {
            Assert.IsFalse(GeneratedScopes.IsRegistered(""));
            Assert.IsFalse(GeneratedScopes.IsRegistered(null));
            Assert.IsFalse(GeneratedScopes.Unregister(""));
            Assert.IsFalse(GeneratedScopes.Unregister(null));
        }

        [Test]
        public void RootId_MethodGroup_IsTypeWithDotsAndMethod()
        {
            Action root = RootMethod.Run;

            Assert.AreEqual("Internal.Tests.GeneratedScopesTests.RootMethod.Run", GeneratedScopes.RootId(root.Method));
        }

        [Test]
        public void RootId_IsBuiltOnce()
        {
            Action root = RootMethod.Run;

            Assert.AreSame(GeneratedScopes.RootId(root.Method), GeneratedScopes.RootId(root.Method));
        }

        [Test]
        public void RootId_LocalFunction_IsOuterMethodPlusName()
        {
            Action root = LocalRoot;

            Assert.AreEqual(
                "Internal.Tests.GeneratedScopesTests." + nameof(RootId_LocalFunction_IsOuterMethodPlusName) + "+LocalRoot",
                GeneratedScopes.RootId(root.Method));

            static void LocalRoot()
            {
            }
        }

        // A capturing local function lives in a compiler class: the id still names the type of the source.
        [Test]
        public void RootId_CapturingLocalFunction_SkipsTheCompilerClass()
        {
            var calls = 0;
            Action root = CapturingRoot;

            Assert.AreEqual(
                "Internal.Tests.GeneratedScopesTests." + nameof(RootId_CapturingLocalFunction_SkipsTheCompilerClass) +
                "+CapturingRoot",
                GeneratedScopes.RootId(root.Method));

            void CapturingRoot()
            {
                calls++;
            }
        }

        [Test]
        public void RootId_Lambda_Throws()
        {
            Action root = () => { };

            var exception = Assert.Throws<ArgumentException>(() => GeneratedScopes.RootId(root.Method));

            StringAssert.Contains("not a lambda", exception.Message);
        }

        [Test]
        public void RootId_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GeneratedScopes.RootId(null));
        }

        [Test]
        public void VariantKey_IsRootAndViewType_BuiltOnce()
        {
            var key = GeneratedScopes.VariantKey("Root.Id", typeof(FirstView));

            Assert.AreEqual("Root.Id@Internal.Tests.GeneratedScopesTests.FirstView", key);
            Assert.AreSame(key, GeneratedScopes.VariantKey("Root.Id", typeof(FirstView)));
        }

        [Test]
        public void RegisterVariant_RegistersTheKeyAndNotTheRoot()
        {
            var root = NewRoot();
            var key = GeneratedScopes.VariantKey(root, typeof(FirstView));
            _roots.Add(key);

            Assert.IsFalse(GeneratedScopes.HasVariants(root));

            GeneratedScopes.RegisterVariant(root, typeof(FirstView), FirstFactory, NoTypes, NoTypes);

            Assert.IsTrue(GeneratedScopes.HasVariants(root));
            Assert.IsTrue(GeneratedScopes.IsRegistered(key));
            Assert.IsFalse(GeneratedScopes.IsRegistered(root));
        }

        [Test]
        public void RegisterVariant_WithoutViewType_Throws()
        {
            var root = NewRoot();

            Assert.Throws<ArgumentNullException>(
                () => GeneratedScopes.RegisterVariant(root, null, FirstFactory, NoTypes, NoTypes));
            Assert.IsFalse(GeneratedScopes.HasVariants(root));
        }

        [Test]
        public void CreateEntity_RootWithoutVariants_BuildsTheRoot()
        {
            var root = NewRoot();
            string built = null;
            GeneratedScopes.Register(root, request => Record(request, out built), NoTypes, NoTypes);

            ScopeContainer.CreateEntity(root, typeof(FirstView), new ContainerBuilder());

            Assert.AreEqual(root, built);
        }

        [Test]
        public void CreateEntity_RootWithVariants_BuildsTheClassOfTheView()
        {
            var root = NewRoot();
            string built = null;
            var first = GeneratedScopes.VariantKey(root, typeof(FirstView));
            var second = GeneratedScopes.VariantKey(root, typeof(SecondView));
            _roots.Add(first);
            _roots.Add(second);
            GeneratedScopes.RegisterVariant(root, typeof(FirstView), request => Record(request, out built), NoTypes, NoTypes);
            GeneratedScopes.RegisterVariant(root, typeof(SecondView), request => Record(request, out built), NoTypes, NoTypes);

            ScopeContainer.CreateEntity(root, typeof(SecondView), new ContainerBuilder());

            Assert.AreEqual(second, built);
        }

        // A view type without its variant is a stale asset map: the root is never taken instead.
        [Test]
        public void CreateEntity_ViewWithoutVariant_Throws()
        {
            var root = NewRoot();
            _roots.Add(GeneratedScopes.VariantKey(root, typeof(FirstView)));
            GeneratedScopes.Register(root, FirstFactory, NoTypes, NoTypes);
            GeneratedScopes.RegisterVariant(root, typeof(FirstView), FirstFactory, NoTypes, NoTypes);

            var exception = Assert.Throws<InvalidOperationException>(
                () => ScopeContainer.CreateEntity(root, typeof(SecondView), new ContainerBuilder()));

            StringAssert.Contains(typeof(SecondView).FullName, exception.Message);
        }

        [Test]
        public void Create_UnknownRoot_Throws()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => ScopeContainer.Create("Unknown.Root", new ContainerBuilder()));

            StringAssert.Contains("Unknown.Root", exception.Message);
            StringAssert.Contains("CINGR", exception.Message);
        }

        [Test]
        public void Create_RunsTheFactoryWithTheRequestOfTheBuilder()
        {
            var parent = new GeneratedScopeProbe(new Lifetime());
            var scope = new Lifetime();
            var builder = new ContainerBuilder("Scope name", parent, scope);
            var root = NewRoot();
            GeneratedScopeRequest taken = default;
            GeneratedScopes.Register(root, request =>
            {
                taken = request;
                return new GeneratedScopeProbe(request.Lifetime);
            }, NoTypes, NoTypes);

            var created = ScopeContainer.Create(root, builder);

            Assert.AreEqual(root, taken.RootId);
            Assert.AreEqual("Scope name", taken.Name);
            Assert.AreSame(parent, taken.Parent);
            Assert.AreSame(taken.Lifetime, created.Lifetime);
        }

        [Test]
        public void Create_FactoryThrows_TerminatesTheLifetimeAndLeavesTheBuilderOpen()
        {
            var builder = new ContainerBuilder();
            IReadOnlyLifetime lifetime = null;

            var exception = Assert.Throws<ArithmeticException>(() => GeneratedScopeProbe.Build(builder, request =>
            {
                lifetime = request.Lifetime;
                throw new ArithmeticException("factory failed");
            }));

            Assert.AreEqual("factory failed", exception.Message);
            Assert.IsTrue(lifetime.IsTerminated);
            Assert.DoesNotThrow(() => builder.AddInjection(new object()));
        }

        [Test]
        public void Create_FactoryReturnsNull_TerminatesTheLifetimeAndThrows()
        {
            var builder = new ContainerBuilder();
            IReadOnlyLifetime lifetime = null;

            var exception = Assert.Throws<InvalidOperationException>(() => GeneratedScopeProbe.Build(builder, request =>
            {
                lifetime = request.Lifetime;
                return null;
            }));

            StringAssert.Contains("returned null", exception.Message);
            Assert.IsTrue(lifetime.IsTerminated);
        }

        [Test]
        public void Create_Diagnostics_GoesToTheRegistryUntilTheEndOfTheContainer()
        {
            var scope = new Lifetime();
            var builder = new ContainerBuilder("Scope", scope);
            var diagnostics = new ContainerDiagnostics("Scope", null, null, null);
            ContainerRegistryDebug.RecordLoadedAsset(scope, "label", "group");

            var created = GeneratedScopeProbe.Build(builder,
                request => new GeneratedScopeProbe(request.Lifetime, diagnostics));

            Assert.AreEqual(1, diagnostics.LoadedAssets.Count);
            Assert.IsTrue(ContainerRegistryDebug.Roots.Contains(diagnostics));

            created.Dispose();

            Assert.IsFalse(ContainerRegistryDebug.Roots.Contains(diagnostics));
        }

        [Test]
        public void ThrowDisposeErrors_Null_DoesNothing()
        {
            Assert.DoesNotThrow(() => GeneratedScopes.ThrowDisposeErrors(null));
        }

        [Test]
        public void ThrowDisposeErrors_One_RethrowsItAsItIs()
        {
            var error = new ArithmeticException();

            var thrown = Assert.Throws<ArithmeticException>(
                () => GeneratedScopes.ThrowDisposeErrors(new List<Exception> { error }));

            Assert.AreSame(error, thrown);
        }

        [Test]
        public void ThrowDisposeErrors_Several_ThrowsThemTogether()
        {
            var first = new ArithmeticException();
            var second = new FormatException();

            var thrown = Assert.Throws<AggregateException>(
                () => GeneratedScopes.ThrowDisposeErrors(new List<Exception> { first, second }));

            CollectionAssert.AreEqual(new Exception[] { first, second }, thrown.InnerExceptions);
        }

        [Test]
        public void RequireAlternative_ReturnsTheValue_ThrowsWithoutIt()
        {
            var value = new object();

            Assert.AreSame(value, GeneratedScopes.RequireAlternative(value, "Some.Type"));

            var exception = Assert.Throws<InvalidOperationException>(
                () => GeneratedScopes.RequireAlternative<object>(null, "Some.Type"));

            StringAssert.Contains("Some.Type", exception.Message);
        }

        [Test]
        public void WithoutAbsent_NothingAbsent_ReturnsTheSameArray()
        {
            var values = new[] { new object(), new object() };

            Assert.AreSame(values, GeneratedScopes.WithoutAbsent(values));
        }

        [Test]
        public void WithoutAbsent_KeepsTheOrderOfThePresent()
        {
            var first = new object();
            var second = new object();

            var result = GeneratedScopes.WithoutAbsent(new[] { null, first, null, second, null });

            CollectionAssert.AreEqual(new[] { first, second }, result);
        }

        private static readonly Type[] NoTypes = Array.Empty<Type>();

        private string NewRoot()
        {
            var root = "GeneratedScopesTests." + Guid.NewGuid();
            _roots.Add(root);
            return root;
        }

        private static IContainer FirstFactory(GeneratedScopeRequest request)
        {
            return new GeneratedScopeProbe(request.Lifetime);
        }

        private static IContainer SecondFactory(GeneratedScopeRequest request)
        {
            return new GeneratedScopeProbe(request.Lifetime);
        }

        private static IContainer Record(GeneratedScopeRequest request, out string rootId)
        {
            rootId = request.RootId;
            return new GeneratedScopeProbe(request.Lifetime);
        }

        private static class RootMethod
        {
            public static void Run()
            {
            }
        }

        private sealed class FirstView
        {
        }

        private sealed class SecondView
        {
        }
    }
}
