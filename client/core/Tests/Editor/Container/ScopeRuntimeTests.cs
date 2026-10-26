using System;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    // The generated class built from a real ContainerBuilder: runtime values are bound to their registration.
    [Category("Container")]
    public class ScopeRuntimeTests
    {
        [Test]
        public void WithParameter_OfOneType_GoesToItsOwnRegistration()
        {
            ScopeRuntimeRoots.FirstConfig = new ScopeRuntimeConfig();
            ScopeRuntimeRoots.SecondConfig = new ScopeRuntimeConfig();

            var container = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Parameters);

            Assert.AreSame(ScopeRuntimeRoots.FirstConfig, container.Resolve<ScopeRuntimeFirstConsumer>().Config);
            Assert.AreSame(ScopeRuntimeRoots.SecondConfig, container.Resolve<ScopeRuntimeSecondConsumer>().Config);
            container.Dispose();
        }

        [Test]
        public void RegisterInstance_TwiceOfOneType_ListsBoth()
        {
            ScopeRuntimeRoots.FirstDep = new ScopeRuntimeDep();
            ScopeRuntimeRoots.SecondDep = new ScopeRuntimeDep();

            var container = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Instances);
            var deps = container.Resolve<ScopeRuntimeDepList>().Deps;

            Assert.AreEqual(2, deps.Count);
            Assert.AreSame(ScopeRuntimeRoots.FirstDep, deps[0]);
            Assert.AreSame(ScopeRuntimeRoots.SecondDep, deps[1]);
            container.Dispose();
        }

        [Test]
        public void RegisterComponent_TwiceOfOneType_ListsBoth()
        {
            ScopeRuntimeRoots.FirstAsset = ScriptableObject.CreateInstance<ScopeRuntimeAsset>();
            ScopeRuntimeRoots.SecondAsset = ScriptableObject.CreateInstance<ScopeRuntimeAsset>();

            var container = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Components);
            var assets = container.Resolve<ScopeRuntimeAssetList>().Assets;

            Assert.AreEqual(2, assets.Count);
            Assert.AreSame(ScopeRuntimeRoots.FirstAsset, assets[0]);
            Assert.AreSame(ScopeRuntimeRoots.SecondAsset, assets[1]);
            container.Dispose();
            UnityEngine.Object.DestroyImmediate(ScopeRuntimeRoots.FirstAsset);
            UnityEngine.Object.DestroyImmediate(ScopeRuntimeRoots.SecondAsset);
        }

        // One generated slot cannot hold the instances of a loop: building must fail instead of keeping one of them.
        [Test]
        public void RegisterInstance_InLoop_Throws()
        {
            ScopeRuntimeRoots.LoopDeps = new IScopeRuntimeDep[] { new ScopeRuntimeDep(), new ScopeRuntimeDep() };

            var exception = Assert.Throws<InvalidOperationException>(
                () => GeneratedScopeHarness.Build(ScopeRuntimeRoots.InstanceLoop));

            StringAssert.Contains(typeof(IScopeRuntimeDep).FullName, exception.Message);
        }

        // A registration the generator could not see (a call through an interface) must not vanish silently.
        [Test]
        public void Registration_UnknownToGenerator_Throws()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => GeneratedScopeHarness.Build(ScopeRuntimeRoots.Hidden));

            StringAssert.Contains(typeof(ScopeRuntimeHiddenService).FullName, exception.Message);
        }

        // [Inject] of a base class is the [Inject] of the registered type.
        [Test]
        public void BaseClassInject_IsCalled()
        {
            ScopeRuntimeRoots.FirstConfig = new ScopeRuntimeConfig();

            var container = GeneratedScopeHarness.Build(ScopeRuntimeRoots.BaseInject);

            Assert.AreSame(ScopeRuntimeRoots.FirstConfig, container.Resolve<ScopeRuntimeDerivedInject>().Config);
            container.Dispose();
        }

        [Test]
        public void ConditionalRegistration_NotTaken_ScopeBuilds()
        {
            ScopeRuntimeRoots.IsOptionalRegistered = false;

            var container = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Optional);

            Assert.IsNotNull(container.Resolve<ScopeRuntimeDep>());
            Assert.IsFalse(container.TryResolve(typeof(ScopeRuntimeOptional), out _));
            Assert.Throws<InvalidOperationException>(() => container.Resolve<ScopeRuntimeOptional>());
            container.Dispose();
        }

        [Test]
        public void ConditionalRegistration_Taken_Resolves()
        {
            ScopeRuntimeRoots.IsOptionalRegistered = true;

            var container = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Optional);

            Assert.IsNotNull(container.Resolve<ScopeRuntimeOptional>());
            container.Dispose();
        }

        // Lifetime listeners see live services; a throwing Dispose does not skip the others, the error is rethrown.
        [Test]
        public void Dispose_ServiceThrows_DisposesTheRestAndRethrows()
        {
            var container = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Disposal);
            var tracked = container.Resolve<ScopeRuntimeTrackedDisposable>();
            var disposedInListener = true;
            container.Lifetime.Listen(() => disposedInListener = tracked.IsDisposed);

            var exception = Assert.Throws<InvalidOperationException>(container.Dispose);

            Assert.AreEqual("dispose failed", exception.Message);
            Assert.IsTrue(tracked.IsDisposed, "a throwing service skipped the rest");
            Assert.IsTrue(container.Lifetime.IsTerminated, "a throwing service skipped the lifetime");
            Assert.IsFalse(disposedInListener, "lifetime listeners ran after the services were disposed");
        }

        [Test]
        public void Resolve_AfterDispose_Throws()
        {
            var container = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            container.Dispose();

            Assert.Throws<ObjectDisposedException>(() => container.Resolve<IEventLoop>());
        }
    }
}
