namespace PlayerAI.States
{
    public class IdleState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Idle;

        public void Enter(PlayerStateMachine ctx)
        {
            if (ctx.Rb != null)
            {
                ctx.Rb.linearVelocity = UnityEngine.Vector3.zero;
            }
        }

        public void Tick(PlayerStateMachine ctx) { }

        public void FixedTick(PlayerStateMachine ctx)
        {
            if (ctx.Rb != null)
            {
                ctx.Rb.linearVelocity = UnityEngine.Vector3.zero;
            }
        }

        public void Exit(PlayerStateMachine ctx) { }

        public PlayerStateType? CheckTransitions(PlayerStateMachine ctx)
        {
            if (ctx.Stats != null && ctx.Stats.IsDowned)
                return PlayerStateType.Downed;

            if (ctx.HasMoveInput)
                return PlayerStateType.Moving;

            if (ctx.Stats != null && ctx.Stats.IsInvincible)
                return PlayerStateType.Damaged;

            return null;
        }
    }
}
