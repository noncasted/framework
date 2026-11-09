using Cysharp.Threading.Tasks;
using Internal.EditorTests.Content;

namespace Internal.Tests
{
    // The scopes of ContentScopeTests: a root with the content loader, a child that loads its content and its scene.
    public static class ContentScopeRoots
    {
        public static ContentTestTarget Loaded { get; private set; }
        public static ContentSlot<ContentTestTarget> Requested { get; private set; }

        public static void Reset()
        {
            Loaded = null;
            Requested = null;
        }

        public static class Root
        {
            public static UniTask Construct(IScopeBuilder builder, ContentLoader loader)
            {
                builder.RegisterInstance(loader).As<IContentLoader>();
                return UniTask.CompletedTask;
            }
        }

        public static class AddedRoot
        {
            public static UniTask Construct(IScopeBuilder builder, ContentOptions options)
            {
                return builder.AddContent(options);
            }
        }

        public static class Empty
        {
            public static UniTask Construct(IScopeBuilder builder)
            {
                return UniTask.CompletedTask;
            }
        }

        public static class Child
        {
            public static async UniTask Construct(IScopeBuilder builder, ContentTestReferences references)
            {
                Loaded = await builder.LoadContent(references.Target);
                builder.RequestContent(references.Target, out var requested);
                Requested = requested;
            }
        }
    }
}
