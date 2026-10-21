using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public interface IRemoteState
    {
        IStateDefinition Definition { get; }

        UniTask Enter(IReadOnlyLifetime lifetime, IStatePayload payload);
    }
}
