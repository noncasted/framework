using System;
using System.Reflection;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public class ScopeLoadOptions
    {
        public ScopeLoadOptions(ILoadedScope parent, Func<IScopeBuilder, UniTask> constructCallback)
            : this(parent, constructCallback, constructCallback.Method)
        {
        }

        private ScopeLoadOptions(
            ILoadedScope parent,
            Func<IScopeBuilder, UniTask> constructCallback,
            MethodInfo root)
        {
            Parent = parent;
            ConstructCallback = constructCallback;
            RootId = GeneratedScopes.RootId(root);
        }

        /// <summary>
        /// Root with an argument. The root is passed as a method group rather than a lambda: it is used
        /// to select the generated scope class.
        /// </summary>
        public static ScopeLoadOptions Create<TArg>(
            ILoadedScope parent,
            Func<IScopeBuilder, TArg, UniTask> construct,
            TArg arg)
        {
            return new ScopeLoadOptions(parent, builder => construct(builder, arg), construct.Method);
        }

        public ILoadedScope Parent { get; }
        public Func<IScopeBuilder, UniTask> ConstructCallback { get; }
        public string RootId { get; }
        public IScopeServiceSceneLoader SceneLoader { get; private set; } = new NoScene();
        public bool IsMock { get; private set; }

        /// <summary>
        /// The scope is part of the mock.
        /// </summary>
        public ScopeLoadOptions AsMock()
        {
            IsMock = true;
            return this;
        }

        public ScopeLoadOptions WithRuntimeScene(string name)
        {
            SceneLoader = new RuntimeScene(name);
            return this;
        }

        public ScopeLoadOptions WithAssetScene(ContentScene scene)
        {
            SceneLoader = new ContentAssetScene(scene);
            return this;
        }
    }
}