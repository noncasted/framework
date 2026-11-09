using System;

namespace ContainerGenerator.Verify {
    // The content of a scope: LoadContent / RequestContent load and register nothing, the scene anchor takes a
    // ContentScene (Scenes.X) as well as an AssetReference (Scenes.X.Value).
    internal static partial class Program {
        private static void RunContentCases() {
            Run("LoadContent and RequestContent are content primitives by the attribute", TestContentPrimitives);
            Run("LoadContent and RequestContent are content primitives by the name when unbound", TestContentPrimitivesByName);
            Run("scene anchor of a ContentScene merges its scene services", TestContentSceneAnchor);
        }

        private static void TestContentPrimitives() {
            var compilation = Compile(ContentRootSource, extraSource: ContentStubs);
            var document = WalkBound(compilation);
            RequireNoDocument(document, "CINGR001");
            RequireNoDocument(document, "CINGR002");
            var root = RequireRoot(document, "ContentRoot.Construct");
            Require(root.Calls.Exists(c => c.IndexOf("Content", StringComparison.Ordinal) >= 0 && c.IndexOf("ContentRoot", StringComparison.Ordinal) < 0) == false,
                "a content load must not be an installer call; calls=" + string.Join(",", root.Calls));
            var references = ReferenceSymbols.Create(compilation);
            var graph = EdgeResolver.ResolveRoot(document, root, compilation, references!);
            RequireNo(graph, "CINGR003");
            FindRegistration(graph, "Service");
            Require(graph.Registrations.Exists(r => r.ImplementationType.IndexOf("Encounter", StringComparison.Ordinal) >= 0 ||
                                                    r.ImplementationType.IndexOf("ContentSlot", StringComparison.Ordinal) >= 0) == false,
                "the loads register nothing; registrations=" +
                                                    string.Join(",", graph.Registrations.ConvertAll(r => r.ImplementationType)));
        }

        private static void TestContentPrimitivesByName() {
            Require(Primitives.Of(null, "LoadContent") == PrimitiveKind.Content, "LoadContent without a symbol");
            Require(Primitives.Of(null, "RequestContent") == PrimitiveKind.Content, "RequestContent without a symbol");
        }

        private static void TestContentSceneAnchor() {
            var compilation = Compile(ContentSceneSource, extraSource: ContentStubs);
            var document = WalkBound(compilation);
            RequireNoDocument(document, "CINGR001");
            var root = RequireRoot(document, "MenuRoot.Construct");
            Require(root.Calls.Exists(c => c.IndexOf("MenuService.Create", StringComparison.Ordinal) >= 0),
                "the scene must merge MenuService.Create; calls=" + string.Join(",", root.Calls));
            var references = ReferenceSymbols.Create(compilation);
            var graph = EdgeResolver.ResolveRoot(document, root, compilation, references!);
            RequireNo(graph, "CINGR003");
            FindRegistration(graph, "MenuThing");
        }

        // The content API of Runtime/Content, with the attributes of the runtime.
        private const string ContentStubs = @"
namespace Internal {
    public class Object { public string name; }
    public class ContentReference<T> where T : Object {}
    public sealed class ContentSlot<T> where T : Object { public T Value => null; }
    public sealed class ContentScene {}
    public static class ContentScopeExtensions {
        [ContainerPrimitive(ContainerPrimitive.Content)]
        public static T LoadContent<T>(this IScopeBuilder builder, ContentReference<T> reference) where T : Object { return null; }
        [ContainerPrimitive(ContainerPrimitive.Content)]
        public static IScopeBuilder RequestContent<T>(this IScopeBuilder builder, ContentReference<T> reference, out ContentSlot<T> slot)
            where T : Object { slot = null; return builder; }
    }
    public static class ScopeBuilderExtensions {
        [ContainerPrimitive(ContainerPrimitive.SceneServices)]
        public static void FindOrLoadSceneWithServices(this IScopeBuilder builder, ContentScene scene, bool isMain = false) {}
    }
}
";

        private const string ContentRootSource = Usings + @"
namespace Sample {
    public class Encounter : Internal.Object {}
    public class Service {}
    public static class Level { public static ContentReference<Encounter> Encounter = new ContentReference<Encounter>(); }
    public static class ContentRoot {
        public static void Construct(IScopeBuilder builder) {
            var encounter = builder.LoadContent(Level.Encounter);
            builder.RequestContent(Level.Encounter, out var slot);
            builder.Register<Service>();
        }
    }
}";

        private const string ContentSceneSource = Usings + @"
[assembly: Internal.ContainerGraphAsset(""Assets/Scenes/Menu.unity"", ""Internal.SceneServicesFactory"", new string[] { ""Sample.MenuService"" })]

namespace Sample {
    public class MenuThing {}
    public class MenuService : ISceneService {
        public void Create(IScopeBuilder builder) { builder.Register<MenuThing>(); }
    }
    public static class Scenes { public static ContentScene Menu = new ContentScene(); }
    public static class MenuRoot {
        public static void Construct(IScopeBuilder builder) {
            builder.FindOrLoadSceneWithServices(Scenes.Menu);
        }
    }
}";
    }
}
