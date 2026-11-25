namespace Internal.Tests
{
    // Entity scope with 50 registrations for measuring mass creation (GeneratedContainer_ManyScopes).
    // 40 Singleton + 10 Transient; the first 10 take parent services (Match/Root), the rest take siblings
    // within the scope. Not part of the main BenchmarkGraph graph.
    internal static class BenchmarkLargeRoots
    {
        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(BenchmarkRoots), nameof(BenchmarkRoots.Match))]
        public static void Card50(IBuilder builder)
        {
            builder.Register<BenchLarge01>();
            builder.Register<BenchLarge02>();
            builder.Register<BenchLarge03>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge04>().As<IBenchSetup>();
            builder.Register<BenchLarge05>();
            builder.Register<BenchLarge06>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge07>().As<IBenchLoaded>();
            builder.Register<BenchLarge08>().As<IBenchSetup>();
            builder.Register<BenchLarge09>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge10>();
            builder.Register<BenchLarge11>();
            builder.Register<BenchLarge12>().As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchLarge13>();
            builder.Register<BenchLarge14>().As<IBenchLoaded>();
            builder.Register<BenchLarge15>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge16>().As<IBenchSetup>();
            builder.Register<BenchLarge17>();
            builder.Register<BenchLarge18>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge19>();
            builder.Register<BenchLarge20>().As<IBenchSetup>();
            builder.Register<BenchLarge21>().As<IBenchEntityComponent>().As<IBenchLoaded>();
            builder.Register<BenchLarge22>();
            builder.Register<BenchLarge23>();
            builder.Register<BenchLarge24>().As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchLarge25>();
            builder.Register<BenchLarge26>();
            builder.Register<BenchLarge27>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge28>().As<IBenchSetup>().As<IBenchLoaded>();
            builder.Register<BenchLarge29>();
            builder.Register<BenchLarge30>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge31>();
            builder.Register<BenchLarge32>().As<IBenchSetup>();
            builder.Register<BenchLarge33>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge34>();
            builder.Register<BenchLarge35>().As<IBenchLoaded>();
            builder.Register<BenchLarge36>().As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchLarge37>();
            builder.Register<BenchLarge38>();
            builder.Register<BenchLarge39>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge40>().As<IBenchSetup>();
            builder.Register<BenchLarge41>(ServiceLifetime.Transient);
            builder.Register<BenchLarge42>(ServiceLifetime.Transient).As<IBenchEntityComponent>().As<IBenchLoaded>();
            builder.Register<BenchLarge43>(ServiceLifetime.Transient);
            builder.Register<BenchLarge44>(ServiceLifetime.Transient).As<IBenchSetup>();
            builder.Register<BenchLarge45>(ServiceLifetime.Transient).As<IBenchEntityComponent>();
            builder.Register<BenchLarge46>(ServiceLifetime.Transient);
            builder.Register<BenchLarge47>(ServiceLifetime.Transient);
            builder.Register<BenchLarge48>(ServiceLifetime.Transient).As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchLarge49>(ServiceLifetime.Transient).As<IBenchLoaded>();
            builder.Register<BenchLarge50>(ServiceLifetime.Transient);
        }
    }
}
