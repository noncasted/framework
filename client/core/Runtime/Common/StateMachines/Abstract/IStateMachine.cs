using Cysharp.Threading.Tasks;
using Internal;

namespace Internal
{
    public interface IStateMachine
    {
        IViewableDelegate Exited { get; }
        IState Current { get; }

        void Initialize(IReadOnlyLifetime lifetime);
        bool IsAvailable(IState state);
        IStateHandle CreateHandle(IState state);
        IStateHandle RestartHandle(IState state);
        UniTask<(bool, IStateHandle)> TryCreateHandleFromPriorityQueue(IState state);
        void Exit(IState state);
    }
}