using Cysharp.Threading.Tasks;

namespace Internal
{
    /// <summary>
    /// The services scene is needed only as a container for the scope objects, so it is created
    /// on the fly: an empty scene in a bundle costs a file open and a couple of frames of async loading,
    /// while its payload is zero.
    /// </summary>
    public class RuntimeScene : IScopeServiceSceneLoader
    {
        public RuntimeScene(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            var scene = loader.Create(Name);
            return UniTask.FromResult<IServiceScopeBinder>(new ServiceScopeBinder(scene.Scene));
        }
    }
}
