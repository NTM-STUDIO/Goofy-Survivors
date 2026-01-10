using PlayerAI;

namespace PlayerAI.States
{
    public class IdleState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Idle;

        public void Enter(PlayerStateMachine ctx)
        {
            if (ctx.Animator != null)
            {
                ctx.Animator.Play("Idle");
            }

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

            // Don't transition to Damaged from Idle - let MovingState handle it
            // This prevents animation conflicts when taking damage while idle

            return null;
        }
    }
}
