using Internal;

namespace Internal
{
    public interface IStateHandle
    {
        IReadOnlyLifetime Lifetime { get; }

        void Exit();
    }
}