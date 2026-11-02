using System;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    // What the installer writes into ContainerBuilder and what the factory of a generated class takes from it.
    [Category("Container")]
    public class ContainerBuilderTests
    {
        [Test]
        public void Registrations_WithoutArguments_Throw()
        {
            var builder = new ContainerBuilder();
            var asset = ScriptableObject.CreateInstance<ScopeRuntimeAsset>();
            UnityEngine.Object.DestroyImmediate(asset);

            Assert.Throws<ArgumentNullException>(() => builder.Add(null, ServiceLifetime.Singleton));
            Assert.Throws<ArgumentNullException>(() => builder.AddInstance(null, new Dep()));
            Assert.Throws<ArgumentNullException>(() => builder.AddInstance(typeof(Dep), null));
            Assert.Throws<ArgumentNullException>(() => builder.AddComponent(null, null, ServiceLifetime.Singleton));
            Assert.Throws<ArgumentNullException>(() => builder.AddComponent(typeof(ScopeRuntimeAsset), asset, ServiceLifetime.Singleton));
            Assert.Throws<ArgumentNullException>(() => builder.AddInjection(null));
        }

        [Test]
        public void Constructor_WithoutParent_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ContainerBuilder("Scope", null, new Lifetime()));
        }

        [Test]
        public void Name_IsRootByDefault()
        {
            Assert.AreEqual("Root", new ContainerBuilder().Name);
        }

        [Test]
        public void ImplementationType_IsTheRegisteredOrTheTypeOfTheInstance()
        {
            var builder = new ContainerBuilder();

            Assert.AreEqual(typeof(Service), builder.Add(typeof(Service), ServiceLifetime.Singleton).ImplementationType);
            Assert.AreEqual(typeof(Dep), builder.AddInstance(typeof(IDep), new Dep()).ImplementationType);
        }

        [Test]
        public void AddServiceType_WithoutType_Throws()
        {
            var registration = new ContainerBuilder().Add(typeof(Service), ServiceLifetime.Singleton);

            Assert.Throws<ArgumentNullException>(() => registration.AddServiceType(null));
            Assert.DoesNotThrow(() => registration.AddServiceType(typeof(Service)));
        }

        [Test]
        public void SetParameter_WithoutType_Throws()
        {
            var registration = new ContainerBuilder().Add(typeof(Service), ServiceLifetime.Singleton);

            Assert.Throws<ArgumentNullException>(() => registration.SetParameter(null, new Dep()));
        }

        [Test]
        public void Registration_Builder_IsTheAttachedOne()
        {
            var containerBuilder = new ContainerBuilder();
            var builder = new RootBuilder(containerBuilder, new EventLoop(), new Lifetime());

            Assert.AreSame(builder, containerBuilder.Add(typeof(Service), ServiceLifetime.Singleton).Builder);
        }

        [Test]
        public void AttachingASecondBuilder_Throws()
        {
            var containerBuilder = new ContainerBuilder("Scope");
            new RootBuilder(containerBuilder, new EventLoop(), new Lifetime());

            var exception = Assert.Throws<InvalidOperationException>(
                () => new RootBuilder(containerBuilder, new EventLoop(), new Lifetime()));

            StringAssert.Contains("Scope", exception.Message);
        }

        [Test]
        public void GetInstance_TakesTheOccurrenceOfTheDeclaredType()
        {
            var builder = new ContainerBuilder();
            var first = new Dep();
            var second = new Dep();
            var other = new Dep();
            builder.AddInstance(typeof(Dep), first);
            builder.AddInstance(typeof(IDep), other);
            builder.AddInstance(typeof(Dep), second);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                Assert.AreSame(first, request.GetInstance<Dep>(0));
                Assert.AreSame(second, request.GetInstance<Dep>(1));
                Assert.AreSame(other, request.GetInstance<IDep>(0));
            }, instances: new[] { typeof(Dep), typeof(IDep), typeof(Dep) });
        }

        [Test]
        public void GetInstance_MoreThanRegistered_Throws()
        {
            var builder = new ContainerBuilder("Scope");
            builder.AddInstance(typeof(Dep), new Dep());

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                var exception = Assert.Throws<InvalidOperationException>(() => request.GetInstance<Dep>(1));

                StringAssert.Contains("Scope", exception.Message);
                StringAssert.Contains("1 instance(s)", exception.Message);
            }, instances: new[] { typeof(Dep), typeof(Dep) });
        }

        [Test]
        public void GetInstance_Component_IsTakenByDeclaredType()
        {
            var builder = new ContainerBuilder();
            var asset = ScriptableObject.CreateInstance<ScopeRuntimeAsset>();
            builder.AddComponent(typeof(ScopeRuntimeAsset), asset, ServiceLifetime.Singleton);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                Assert.AreSame(asset, request.GetInstance<ScopeRuntimeAsset>(0));
                Assert.IsTrue(request.IsRegistered(typeof(ScopeRuntimeAsset)));
            }, instances: new[] { typeof(ScopeRuntimeAsset) });

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void GetParameter_IsBoundToTheOccurrenceOfTheImplementation()
        {
            var builder = new ContainerBuilder();
            var first = new Dep();
            var second = new Dep();
            builder.Add(typeof(Service), ServiceLifetime.Singleton).SetParameter(typeof(Dep), first);
            builder.Add(typeof(OtherService), ServiceLifetime.Singleton);
            builder.Add(typeof(Service), ServiceLifetime.Singleton).SetParameter(typeof(Dep), second);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                Assert.AreSame(first, request.GetParameter<Dep>(typeof(Service), 0));
                Assert.AreSame(second, request.GetParameter<Dep>(typeof(Service), 1));
            }, constructed: new[] { typeof(Service), typeof(OtherService) });
        }

        [Test]
        public void GetParameter_AnInstanceOfTheImplementationIsNotAnOccurrence()
        {
            var builder = new ContainerBuilder();
            var dep = new Dep();
            builder.AddInstance(typeof(Service), new Service());
            builder.Add(typeof(Service), ServiceLifetime.Singleton).SetParameter(typeof(Dep), dep);

            GeneratedScopeProbe.Inspect(builder,
                request => Assert.AreSame(dep, request.GetParameter<Dep>(typeof(Service), 0)),
                constructed: new[] { typeof(Service) },
                instances: new[] { typeof(Service) });
        }

        [Test]
        public void SetParameter_SameTypeAgain_ReplacesTheValue()
        {
            var builder = new ContainerBuilder();
            var second = new Dep();
            builder.Add(typeof(Service), ServiceLifetime.Singleton)
                .SetParameter(typeof(Dep), new Dep())
                .SetParameter(typeof(Dep), second);

            GeneratedScopeProbe.Inspect(builder,
                request => Assert.AreSame(second, request.GetParameter<Dep>(typeof(Service), 0)),
                constructed: new[] { typeof(Service) });
        }

        [Test]
        public void GetParameter_ExactTypeWins_OtherwiseAnAssignableOne()
        {
            var builder = new ContainerBuilder();
            var assignable = new Dep();
            var exact = new Dep();
            builder.Add(typeof(Service), ServiceLifetime.Singleton)
                .SetParameter(typeof(Dep), assignable)
                .SetParameter(typeof(IDep), exact);
            builder.Add(typeof(OtherService), ServiceLifetime.Singleton).SetParameter(typeof(Dep), assignable);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                Assert.AreSame(exact, request.GetParameter<IDep>(typeof(Service), 0));
                Assert.AreSame(assignable, request.GetParameter<IDep>(typeof(OtherService), 0));
            }, constructed: new[] { typeof(Service), typeof(OtherService) });
        }

        [Test]
        public void GetParameter_WithoutValue_TakesTheParent()
        {
            var fromParent = new Dep();
            var parent = new GeneratedScopeProbe(new Lifetime(), null, fromParent);
            var builder = new ContainerBuilder("Scope", parent, new Lifetime());
            builder.Add(typeof(Service), ServiceLifetime.Singleton);

            GeneratedScopeProbe.Inspect(builder,
                request => Assert.AreSame(fromParent, request.GetParameter<Dep>(typeof(Service), 0)),
                constructed: new[] { typeof(Service) });
        }

        [Test]
        public void GetParameter_NeitherValueNorParent_Throws()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(Service), ServiceLifetime.Singleton);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => request.GetParameter<Dep>(typeof(Service), 0));

                StringAssert.Contains("neither WithParameter", exception.Message);
            }, constructed: new[] { typeof(Service) });
        }

        [Test]
        public void GetParameter_MoreThanRegistered_Throws()
        {
            var builder = new ContainerBuilder("Scope");
            builder.Add(typeof(Service), ServiceLifetime.Singleton);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => request.GetParameter<Dep>(typeof(Service), 1));

                StringAssert.Contains("Scope", exception.Message);
                StringAssert.Contains("1 registration(s)", exception.Message);
                Assert.Throws<ArgumentNullException>(() => request.GetParameter<Dep>(null, 0));
            }, constructed: new[] { typeof(Service) });
        }

        [Test]
        public void IsRegistered_LooksAtTheImplementations()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(Service), ServiceLifetime.Singleton);
            builder.AddInstance(typeof(IDep), new Dep());

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                Assert.IsTrue(request.IsRegistered(typeof(Service)));
                Assert.IsTrue(request.IsRegistered(typeof(Dep)));
                Assert.IsFalse(request.IsRegistered(typeof(OtherService)));
                Assert.Throws<ArgumentNullException>(() => request.IsRegistered(null));
            }, constructed: new[] { typeof(Service) }, instances: new[] { typeof(IDep) });
        }

        [Test]
        public void Get_LastRegistrationWins()
        {
            var builder = new ContainerBuilder();
            var last = new Dep();
            builder.AddInstance(typeof(Dep), new Dep());
            builder.AddInstance(typeof(Dep), last);

            GeneratedScopeProbe.Inspect(builder,
                request => Assert.AreSame(last, request.Get<Dep>()),
                instances: new[] { typeof(Dep), typeof(Dep) });
        }

        [Test]
        public void Get_InARegistration_TheInstanceWinsOverTheParameter()
        {
            var builder = new ContainerBuilder();
            var instance = new Dep();
            builder.AddInstance(typeof(Dep), instance).SetParameter(typeof(Dep), new Dep());

            GeneratedScopeProbe.Inspect(builder,
                request => Assert.AreSame(instance, request.Get<Dep>()),
                instances: new[] { typeof(Dep) });
        }

        [Test]
        public void Get_Parameter_MatchesOnlyItsOwnType()
        {
            var builder = new ContainerBuilder();
            var dep = new Dep();
            builder.Add(typeof(Service), ServiceLifetime.Singleton).SetParameter(typeof(Dep), dep);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                Assert.AreSame(dep, request.Get<Dep>());
                Assert.IsFalse(request.TryGet(typeof(IDep), out _));
            }, constructed: new[] { typeof(Service) });
        }

        [Test]
        public void Get_RegistrationWinsOverInjection_InjectionWinsOverParent()
        {
            var parent = new GeneratedScopeProbe(new Lifetime(), null, new Dep(), new Service());
            var builder = new ContainerBuilder("Scope", parent, new Lifetime());
            var registered = new Dep();
            var injected = new Service();
            builder.AddInjection(new Dep());
            builder.AddInstance(typeof(Dep), registered);
            builder.AddInjection(injected);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                Assert.AreSame(registered, request.Get<Dep>());
                Assert.AreSame(injected, request.Get<Service>());
            }, instances: new[] { typeof(Dep) });
        }

        [Test]
        public void Get_Nothing_Throws()
        {
            GeneratedScopeProbe.Inspect(new ContainerBuilder(), request =>
            {
                Assert.IsFalse(request.TryGet(typeof(Dep), out var instance));
                Assert.IsNull(instance);
                Assert.Throws<InvalidOperationException>(() => request.Get<Dep>());
                Assert.Throws<ArgumentNullException>(() => request.TryGet(null, out _));
            });
        }

        [Test]
        public void GetFromParent_TakesTheParentOnly()
        {
            var fromParent = new Dep();
            var parent = new GeneratedScopeProbe(new Lifetime(), null, fromParent);
            var builder = new ContainerBuilder("Scope", parent, new Lifetime());
            builder.AddInstance(typeof(Dep), new Dep());

            GeneratedScopeProbe.Inspect(builder,
                request => Assert.AreSame(fromParent, request.GetFromParent<Dep>()),
                instances: new[] { typeof(Dep) });

            GeneratedScopeProbe.Inspect(new ContainerBuilder(),
                request => Assert.Throws<InvalidOperationException>(() => request.GetFromParent<Dep>()));
        }

        [Test]
        public void GetInjections_TakesEveryInstanceOfTheTypeInOrder()
        {
            var builder = new ContainerBuilder();
            var first = new Dep();
            var second = new Dep();
            builder.AddInjection(first);
            builder.AddInjection(new Service());
            builder.AddInjection(second);

            GeneratedScopeProbe.Inspect(builder, request =>
            {
                CollectionAssert.AreEqual(new[] { first, second }, request.GetInjections<Dep>());
                Assert.Throws<InvalidOperationException>(() => request.GetInjections<OtherService>());
            });
        }

        // The check runs before the factory: nothing is built, the builder stays open.
        [Test]
        public void Build_ConstructedRegistrationWithoutSlot_Throws()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(Service), ServiceLifetime.Singleton);
            var built = false;

            var exception = Assert.Throws<InvalidOperationException>(() => GeneratedScopeProbe.Inspect(builder,
                _ => built = true, constructed: new[] { typeof(OtherService) }));

            StringAssert.Contains(typeof(Service).FullName, exception.Message);
            Assert.IsFalse(built);
            Assert.DoesNotThrow(() => builder.AddInjection(new object()));
        }

        [Test]
        public void Build_MoreInstancesThanSlots_Throws()
        {
            var builder = new ContainerBuilder();
            builder.AddInstance(typeof(Dep), new Dep());
            builder.AddInstance(typeof(Dep), new Dep());
            var built = false;

            var exception = Assert.Throws<InvalidOperationException>(() => GeneratedScopeProbe.Inspect(builder,
                _ => built = true, instances: new[] { typeof(Dep), typeof(IDep) }));

            StringAssert.Contains("2 time(s)", exception.Message);
            StringAssert.Contains("1 slot(s)", exception.Message);
            Assert.IsFalse(built);
        }

        // An if without else around a registration leaves a slot empty: that is not an error of the build.
        [Test]
        public void Build_FewerRegistrationsThanSlots_Builds()
        {
            var builder = new ContainerBuilder();
            builder.AddInstance(typeof(Dep), new Dep());
            var built = false;

            GeneratedScopeProbe.Inspect(builder, _ => built = true,
                constructed: new[] { typeof(Service) },
                instances: new[] { typeof(Dep), typeof(Dep) });

            Assert.IsTrue(built);
        }

        [Test]
        public void AfterBuild_Writes_Throw()
        {
            var builder = new ContainerBuilder();
            var registration = builder.Add(typeof(Service), ServiceLifetime.Singleton);
            var asset = ScriptableObject.CreateInstance<ScopeRuntimeAsset>();
            GeneratedScopeProbe.Inspect(builder, _ => { }, constructed: new[] { typeof(Service) });

            AssertClosed(() => builder.Add(typeof(Service), ServiceLifetime.Singleton));
            AssertClosed(() => builder.AddInstance(typeof(Dep), new Dep()));
            AssertClosed(() => builder.AddComponent(typeof(ScopeRuntimeAsset), asset, ServiceLifetime.Singleton));
            AssertClosed(() => builder.AddInjection(new object()));
            AssertClosed(() => registration.AddServiceType(typeof(Service)));
            AssertClosed(() => registration.SetParameter(typeof(Dep), new Dep()));
            AssertClosed(() => _ = registration.ImplementationType);

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void AfterBuild_TheRequest_ReadsNothing()
        {
            var builder = new ContainerBuilder();
            builder.AddInjection(new Dep());
            var request = GeneratedScopeProbe.Inspect(builder, _ => { });

            AssertClosed(() => request.Get<Dep>());
            AssertClosed(() => request.GetInstance<Dep>(0));
            AssertClosed(() => request.GetParameter<Dep>(typeof(Service), 0));
            AssertClosed(() => request.IsRegistered(typeof(Service)));
            AssertClosed(() => request.GetInjections<Dep>());
        }

        // The lists go back to the pool after a build and must come out empty.
        [Test]
        public void NextBuilder_DoesNotSeeTheRegistrationsOfABuiltOne()
        {
            var built = new ContainerBuilder();
            built.Add(typeof(Service), ServiceLifetime.Singleton).SetParameter(typeof(Dep), new Dep());
            built.AddInstance(typeof(OtherService), new OtherService());
            GeneratedScopeProbe.Inspect(built, _ => { },
                constructed: new[] { typeof(Service) },
                instances: new[] { typeof(OtherService) });

            var next = new ContainerBuilder();
            next.Add(typeof(Service), ServiceLifetime.Singleton);

            GeneratedScopeProbe.Inspect(next, request =>
            {
                Assert.IsFalse(request.IsRegistered(typeof(OtherService)));
                Assert.IsFalse(request.TryGet(typeof(Dep), out _));
                Assert.Throws<InvalidOperationException>(() => request.GetParameter<Dep>(typeof(Service), 0));
            }, constructed: new[] { typeof(Service) });
        }

        [Test]
        public void Lifetime_WithoutHost_IsIndependent()
        {
            var request = GeneratedScopeProbe.Inspect(new ContainerBuilder(), _ => { });

            Assert.IsFalse(request.Lifetime.IsTerminated);
        }

        [Test]
        public void Lifetime_OfHost_IsItsChild()
        {
            var host = new Lifetime();
            var request = GeneratedScopeProbe.Inspect(new ContainerBuilder("Scope", host), _ => { });

            Assert.AreNotSame(host, request.Lifetime);

            host.Terminate();

            Assert.IsTrue(request.Lifetime.IsTerminated);
        }

        [Test]
        public void Lifetime_WithScope_IsAChildOfTheScopeNotOfTheParent()
        {
            var parent = new GeneratedScopeProbe(new Lifetime());
            var scope = new Lifetime();
            var request = GeneratedScopeProbe.Inspect(new ContainerBuilder("Scope", parent, scope), _ => { });

            parent.Dispose();
            Assert.IsFalse(request.Lifetime.IsTerminated);

            scope.Terminate();
            Assert.IsTrue(request.Lifetime.IsTerminated);
        }

        [Test]
        public void Lifetime_ParentWithoutScope_IsAChildOfTheParent()
        {
            var parent = new GeneratedScopeProbe(new Lifetime());
            var request = GeneratedScopeProbe.Inspect(new ContainerBuilder("Scope", parent, null), _ => { });

            parent.Dispose();

            Assert.IsTrue(request.Lifetime.IsTerminated);
        }

        private static void AssertClosed(Action action)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => action());

            StringAssert.Contains("Cannot register after", exception.Message);
        }

        private interface IDep
        {
        }

        private sealed class Dep : IDep
        {
        }

        private sealed class Service
        {
        }

        private sealed class OtherService
        {
        }
    }
}
