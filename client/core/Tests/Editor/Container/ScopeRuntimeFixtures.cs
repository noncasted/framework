using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace Internal.Tests
{
    // Roots of the runtime scope tests: what the generated class receives from the builder at runtime.
    // The values come from static fields the tests set before building.
    [NoAutoStaticsCleanup]
    public static class ScopeRuntimeRoots
    {
        public static ScopeRuntimeConfig FirstConfig;
        public static ScopeRuntimeConfig SecondConfig;
        public static IScopeRuntimeDep FirstDep;
        public static IScopeRuntimeDep SecondDep;
        public static ScopeRuntimeAsset FirstAsset;
        public static ScopeRuntimeAsset SecondAsset;
        public static IScopeRuntimeDep[] LoopDeps;
        public static bool IsOptionalRegistered;

        [ContainerGraphRoot]
        public static void Parameters(IBuilder builder)
        {
            builder.Register<ScopeRuntimeFirstConsumer>().WithParameter(FirstConfig);
            builder.Register<ScopeRuntimeSecondConsumer>().WithParameter(SecondConfig);
        }

        [ContainerGraphRoot]
        public static void Instances(IBuilder builder)
        {
            builder.RegisterInstance(FirstDep);
            builder.RegisterInstance(SecondDep);
            builder.Register<ScopeRuntimeDepList>();
        }

        [ContainerGraphRoot]
        public static void Components(IBuilder builder)
        {
            builder.RegisterComponent(FirstAsset);
            builder.RegisterComponent(SecondAsset);
            builder.Register<ScopeRuntimeAssetList>();
        }

        [ContainerGraphRoot]
        public static void InstanceLoop(IBuilder builder)
        {
            foreach (var dep in LoopDeps)
                builder.RegisterInstance(dep);

            builder.Register<ScopeRuntimeDepList>();
        }

        [ContainerGraphRoot]
        public static void Empty(IBuilder builder)
        {
        }

        [ContainerGraphRoot]
        public static void BaseInject(IBuilder builder)
        {
            builder.RegisterInstance(FirstConfig);
            builder.Register<ScopeRuntimeDerivedInject>();
        }

        // An if without else: nothing depends on the optional service, the scope builds either way.
        [ContainerGraphRoot]
        public static void Optional(IBuilder builder)
        {
            if (IsOptionalRegistered == true)
                builder.Register<ScopeRuntimeOptional>();

            builder.Register<ScopeRuntimeDep>();
        }

        // Disposed in reverse construction order: the throwing one first.
        [ContainerGraphRoot]
        public static void Disposal(IBuilder builder)
        {
            builder.Register<ScopeRuntimeTrackedDisposable>();
            builder.Register<ScopeRuntimeThrowingDisposable>();
        }

        // The generator does not follow a call through an interface: what the installer registers is unknown to it.
        [ContainerGraphRoot]
        public static void Hidden(IBuilder builder)
        {
            IScopeRuntimeInstaller installer = new ScopeRuntimeHiddenInstaller();
            installer.Install(builder);
        }
    }

    public sealed class ScopeRuntimeConfig
    {
    }

    public interface IScopeRuntimeDep
    {
    }

    public sealed class ScopeRuntimeDep : IScopeRuntimeDep
    {
    }

    public sealed class ScopeRuntimeFirstConsumer
    {
        public ScopeRuntimeFirstConsumer(ScopeRuntimeConfig config)
        {
            Config = config;
        }

        public ScopeRuntimeConfig Config { get; }
    }

    public sealed class ScopeRuntimeSecondConsumer
    {
        public ScopeRuntimeSecondConsumer(ScopeRuntimeConfig config)
        {
            Config = config;
        }

        public ScopeRuntimeConfig Config { get; }
    }

    public sealed class ScopeRuntimeDepList
    {
        public ScopeRuntimeDepList(IReadOnlyList<IScopeRuntimeDep> deps)
        {
            Deps = deps;
        }

        public IReadOnlyList<IScopeRuntimeDep> Deps { get; }
    }

    public sealed class ScopeRuntimeAssetList
    {
        public ScopeRuntimeAssetList(IReadOnlyList<ScopeRuntimeAsset> assets)
        {
            Assets = assets;
        }

        public IReadOnlyList<ScopeRuntimeAsset> Assets { get; }
    }

    public interface IScopeRuntimeInstaller
    {
        void Install(IBuilder builder);
    }

    public sealed class ScopeRuntimeHiddenInstaller : IScopeRuntimeInstaller
    {
        public void Install(IBuilder builder)
        {
            builder.Register<ScopeRuntimeHiddenService>();
        }
    }

    public sealed class ScopeRuntimeHiddenService
    {
    }

    public abstract class ScopeRuntimeInjectBase
    {
        public ScopeRuntimeConfig Config { get; private set; }

        [Inject]
        public void Setup(ScopeRuntimeConfig config)
        {
            Config = config;
        }
    }

    public sealed class ScopeRuntimeDerivedInject : ScopeRuntimeInjectBase
    {
    }

    public sealed class ScopeRuntimeOptional
    {
    }

    public sealed class ScopeRuntimeTrackedDisposable : System.IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public sealed class ScopeRuntimeThrowingDisposable : System.IDisposable
    {
        public void Dispose()
        {
            throw new System.InvalidOperationException("dispose failed");
        }
    }
}
