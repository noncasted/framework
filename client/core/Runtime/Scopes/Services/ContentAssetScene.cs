using Cysharp.Threading.Tasks;

namespace Internal
{
    // The services scene is a scene of a content bundle (ContentScene) with content the scope needs.
    public class ContentAssetScene : IScopeServiceSceneLoader
    {
        public ContentAssetScene(ContentScene scene)
        {
            _scene = scene;
        }

        private readonly ContentScene _scene;

        public string Name => _scene.Guid;

        public async UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            var scene = await loader.Load(_scene);
            return new ServiceScopeBinder(scene.Scene);
        }
    }
}
