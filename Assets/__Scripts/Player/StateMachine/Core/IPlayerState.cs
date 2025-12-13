namespace PlayerAI
{
    public interface IPlayerState
    {
        PlayerStateType StateType { get; }
        void Enter(PlayerStateMachine ctx);
        void Tick(PlayerStateMachine ctx);
        void FixedTick(PlayerStateMachine ctx);
        void Exit(PlayerStateMachine ctx);
        PlayerStateType? CheckTransitions(PlayerStateMachine ctx);
    }
}
