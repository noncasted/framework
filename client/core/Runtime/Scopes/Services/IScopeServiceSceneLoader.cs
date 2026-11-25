using Cysharp.Threading.Tasks;

namespace Internal
{
    /// <summary>
    /// Where the scope gets the scene for its objects from.
    /// </summary>
    public interface IScopeServiceSceneLoader
    {
        /// <summary>
        /// Scope name in the profiler trace and in the container.
        /// </summary>
        string Name { get; }

        UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader);
    }
}