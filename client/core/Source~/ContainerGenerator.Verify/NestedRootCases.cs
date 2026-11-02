namespace ContainerGenerator.Verify {
    // A root in a nested type: the id of the runtime is the type with dots, so the generated class takes its namespace
    // from the symbol. Each scenario builds the scope through GeneratedScopes, the lookup the runtime makes.
    internal static partial class Program {
        private static void RunNestedRootCases() {
            Run("root in a nested type builds its scope", () => RunScenario(NestedRoot));
            Run("root in a type nested twice builds its scope", () => RunScenario(TwiceNestedRoot));
            Run("roots of one type name in two outer types have two scopes", () => RunScenario(NestedRootsOfOneName));
            Run("local function root in a nested type builds its scope", () => RunScenario(NestedLocalRoot));
            Run("root in a nested type is a parent scope", () => RunScenario(NestedParentRoot));
            Run("root in a nested type and a top level type of a similar name have two scopes", () => RunScenario(NestedRootNextToSimilarName));
            Run("root in a type nested in the global namespace builds its scope", () => RunScenario(NestedRootWithoutNamespace));
        }

        private const string NestedRoot = Usings + @"
namespace Sample {
    public class Service {}
    public static class O1 {
        public static class Root {
            public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(O1.Root.Construct).Resolve<Service>() is Service, ""the scope of the nested root"");
        }
    }
}";

        private const string TwiceNestedRoot = Usings + @"
namespace Sample.Deep {
    public class Service {}
    public class O1 {
        public static class O2 {
            public static class Root {
                public static void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
            }
        }
    }
}
namespace Sample {
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(Deep.O1.O2.Root.Construct).Resolve<Deep.Service>() is Deep.Service, ""the scope of the root"");
        }
    }
}";

        private const string NestedRootsOfOneName = Usings + @"
namespace Sample {
    public class First {}
    public class Second {}
    public static class O1 {
        public static class Root {
            public static void Construct(IScopeBuilder builder) { builder.Register<First>(); }
        }
    }
    public static class O2 {
        public static class Root {
            public static void Construct(IScopeBuilder builder) { builder.Register<Second>(); }
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(O1.Root.Construct).Resolve<First>() is First, ""the scope of O1.Root"");
            Check.True(TestHost.Build(O2.Root.Construct).Resolve<Second>() is Second, ""the scope of O2.Root"");
        }
    }
}";

        private const string NestedLocalRoot = Usings + @"
namespace Sample {
    public class Service {}
    public static class O1 {
        public static class Root {
            public static IContainer Load() {
                return TestHost.Build(Construct);

                void Construct(IScopeBuilder builder) { builder.Register<Service>(); }
            }
        }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(O1.Root.Load().Resolve<Service>() is Service, ""the scope of the local function"");
        }
    }
}";

        private const string NestedParentRoot = Usings + @"
namespace Sample {
    public class Service {}
    public class Needs { public readonly Service Value; public Needs(Service value) { Value = value; } }
    public static class O1 {
        public static class Root {
            public static void Construct(IScopeBuilder builder) { builder.Register<Service>().AsSelfResolvable(); }
        }
    }
    public static class ChildRoot {
        [ContainerScopeParent(typeof(O1.Root), nameof(O1.Root.Construct))]
        public static void Construct(IScopeBuilder builder) { builder.Register<Needs>(); }
    }
    public static class Scenario {
        public static void Run() {
            var parent = TestHost.Build(O1.Root.Construct);
            Check.True(TestHost.Build(ChildRoot.Construct, parent).Resolve<Needs>().Value is Service, ""the child takes it from the parent"");
        }
    }
}";

        private const string NestedRootNextToSimilarName = Usings + @"
namespace Sample {
    public class First {}
    public class Second {}
    public static class O1 {
        public static class Root {
            public static void Construct(IScopeBuilder builder) { builder.Register<First>(); }
        }
    }
    public static class O1Root {
        public static void Construct(IScopeBuilder builder) { builder.Register<Second>(); }
    }
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(O1.Root.Construct).Resolve<First>() is First, ""the scope of O1.Root"");
            Check.True(TestHost.Build(O1Root.Construct).Resolve<Second>() is Second, ""the scope of O1Root"");
        }
    }
}";

        private const string NestedRootWithoutNamespace = Usings + @"
public class GlobalService {}
public static class GlobalOuter {
    public static class Root {
        public static void Construct(IScopeBuilder builder) { builder.Register<GlobalService>(); }
    }
}
namespace Sample {
    public static class Scenario {
        public static void Run() {
            Check.True(TestHost.Build(GlobalOuter.Root.Construct).Resolve<GlobalService>() is GlobalService, ""the scope of the root"");
        }
    }
}";
    }
}
