using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface INetworkSessionSetupCompleted : INetworkSessionCallbackEntry
    {
        UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime);
    }
}
