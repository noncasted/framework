namespace Global.UI
{
    // A window over the match that takes the controls of the local player away while it is visible: the player
    // does not move, aim or fire (PlayerInput reads InputConstraints.Game). One instance per window: the state
    // machine keeps the handle by the state.
    public sealed class GameInputLockState : IUIState
    {
        public IUIConstraints Constraints { get; } = UIConstraints.Game;
    }
}
