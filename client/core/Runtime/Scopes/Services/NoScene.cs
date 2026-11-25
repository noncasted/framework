using Cysharp.Threading.Tasks;

namespace Internal
{
    /// <summary>
    /// The scope has no scene of its own: there is nowhere to move objects, the binder throws when used.
    /// </summary>
    public class NoScene : IScopeServiceSceneLoader
    {
        public string Name => "Registry";

        public UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            return UniTask.FromResult<IServiceScopeBinder>(new ExceptionServiceScopeBinder(Name));
        }
    }
}
