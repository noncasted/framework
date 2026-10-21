using Shared;

namespace Internal
{
    public interface IRemoteStateMachine
    {
        IRemoteState Current { get; }

        void Initialize(IReadOnlyLifetime lifetime);
        void Enter(IStateDefinition definition, IStatePayload payload);
    }
}
