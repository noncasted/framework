using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IState
    {
        IStateDefinition Definition { get; }
        int Priority { get; }

        bool IsAvailable();
        UniTask Enter();
    }
}