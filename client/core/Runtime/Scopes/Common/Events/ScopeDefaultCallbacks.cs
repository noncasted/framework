using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IScopeBaseSetup
    {
        void OnBaseSetup(IReadOnlyLifetime lifetime);
    }

    public interface IScopeBaseSetupAsync
    {
        UniTask OnBaseSetup(IReadOnlyLifetime lifetime);
    }

    public interface IScopeSetup
    {
        void OnSetup(IReadOnlyLifetime lifetime);
    }

    public interface IScopeSetupAsync
    {
        UniTask OnSetup(IReadOnlyLifetime lifetime);
    }

    public interface IScopeSetupCompletion
    {
        void OnSetupCompletion(IReadOnlyLifetime lifetime);
    }

    public interface IScopeSetupCompletionAsync
    {
        UniTask OnSetupCompletion(IReadOnlyLifetime lifetime);
    }

    public interface IScopeLoaded
    {
        void OnLoaded(IReadOnlyLifetime lifetime);
    }

    public interface IScopeLoadedAsync
    {
        UniTask OnLoaded(IReadOnlyLifetime lifetime);
    }

    public interface IScopeDispose
    {
        void OnDispose();
    }

    public interface IScopeDisposeAsync
    {
        UniTask OnDispose();
    }
}